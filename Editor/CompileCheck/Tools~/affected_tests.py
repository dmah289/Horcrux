#!/usr/bin/env python3
"""Chọn test EditMode bị chạm bởi các file đổi từ lượt test trước, để verify.sh không chạy cả assembly mỗi task.

Dùng:
  python -X utf8 -I affected_tests.py --project <gốc> --tests <Asm1,Asm2>   in lựa chọn, mỗi dòng một lệnh cho verify.sh
  python -X utf8 -I affected_tests.py --project <gốc> --record              ghi mốc: HEAD hiện tại của project và submodule

Dòng in ra (cách nhau tab):
  note   <chữ>                  thông tin cho người đọc
  all    <assembly>             chạy cả assembly
  class  <assembly>  <tên lớp>  chạy test có FullName chứa tên lớp

File đổi = `git diff <mốc>` (đã commit và chưa commit) + file chưa track, ở project và mọi submodule. Mốc ở
`Library/HorcruxVerify/tested-at`; verify.sh ghi sau lượt test không có fail mới. Chưa có mốc thì chạy cả assembly.

Chọn bằng tên, không bằng graph dựng sẵn: graph cũ đi sau mỗi lần sửa, và gắn partial class về một file nên sót test.
  .cs     → tên type, extension method khai trong file (bản hiện tại và bản ở mốc) → file khác nhắc tên đó, thấy được
            assembly (cùng assembly hoặc reference trực tiếp) và namespace (cùng, lồng, `using`, ghi đầy đủ) → lặp tới khi
            hết. Test trong tập đó là test bị chạm.
  asset   → script của các object YAML có dòng đổi (m_Script guid → .cs) đi tiếp như .cs; test nhắc tên file asset cũng chạm.
  asmdef, asmref, csc.rsp, Packages/manifest.json, ProjectSettings → chạy cả assembly: đổi biên dịch, không dò theo tên.
"""
import argparse
import collections
import json
import os
import pathlib
import re
import subprocess
import sys

# Mỗi lượt `test run` tốn ~6 s dựng danh sách test và khởi động runner dù chỉ một lớp. Đo 2026-11 trên assembly 496 test
# (5 s là thời gian test thật): cả assembly 23 s một lượt, ba lớp ba lượt cũng 23 s. Quá 2 lớp thì chạy cả assembly.
MAX_CLASS_RUNS = 2
GLOBAL_FILES = re.compile(r'(\.asmdef|\.asmref|csc\.rsp|^Packages/manifest\.json|^ProjectSettings/.*)$')
YAML_EXT = ('.asset', '.prefab', '.unity', '.controller', '.overrideController', '.playable', '.mat')

COMMENT = re.compile(r'//[^\n]*|/\*.*?\*/', re.S)
IDENT = re.compile(r'\b[A-Za-z_]\w*\b')
# Khai báo type kèm thụt đầu dòng: chỉ type ngoài cùng của file mới được file khác gọi tên trần — type lồng gọi qua tên
# type ngoài (`Outer.Inner`), type lồng private thì không ai ngoài gọi được. Lan theo tên type lồng (vd. lớp `Init` lồng
# trong mọi Initializer) thì kéo cả project vào.
DECL = re.compile(r'^([ \t]*)[^\n;{}=()]*?\b(?:(?:class|struct|interface|enum|record)\s+([A-Za-z_]\w*)'
                  r'|delegate\s+[\w<>\[\],.?\s]+?\s([A-Za-z_]\w*)\s*[<(])', re.M)
# Extension method lan theo tên kèm type nhận (`this R`): tên hay chung chung (Init, Show) nên chỉ tính file có nhắc R.
EXT_METHOD = re.compile(r'\bstatic\s+[\w<>\[\],.?\s]+?\s([A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\(\s*this\s+([\w.]+)')
# Lớp cha, interface của type trong file: code gọi qua interface hay lớp cha (DI, kéo thả) không nhắc tên lớp con.
BASES = re.compile(r'\b(?:class|struct|record)\s+[A-Za-z_]\w*\s*(?:<[^>{]*>)?\s*:\s*([^{;]+?)(?:\bwhere\b|\{)')
NAMESPACE = re.compile(r'\bnamespace\s+([\w.]+)')
TEST_ATTR = re.compile(r'\[\s*(?:Test|UnityTest|TestCase\w*)\b')
TOP_CLASS = re.compile(r'^([ \t]*)(?:\[[^\]\n]*\]\s*)*(?:(?:public|internal|sealed|static|partial)\s+)*class\s+([A-Za-z_]\w*)', re.M)
ABSTRACT = re.compile(r'\babstract\s+(?:partial\s+)?class\s+([A-Za-z_]\w*)')
SCRIPT_GUID = re.compile(r'm_Script:\s*\{[^}]*guid:\s*([0-9a-f]{32})')


def git(repo, *args):
    r = subprocess.run(["git", "-C", str(repo), *args], capture_output=True, text=True, encoding="utf8", errors="replace")
    return r.returncode, r.stdout


def repos(project):
    """Project và mọi submodule, đường dẫn tương đối tính từ gốc project ('' là project)."""
    out = [""]
    rc, text = git(project, "submodule", "status", "--recursive")
    if rc == 0:
        out += [line.split()[1] for line in text.splitlines() if len(line.split()) >= 2]
    return out


def base_path(project):
    return project / "Library" / "HorcruxVerify" / "tested-at"


def record(project):
    lines = []
    for rel in repos(project):
        rc, head = git(project / rel, "rev-parse", "HEAD")
        if rc == 0:
            lines.append("%s\t%s" % (rel or ".", head.strip()))
    base_path(project).parent.mkdir(parents=True, exist_ok=True)
    base_path(project).write_text("\n".join(lines) + "\n", encoding="utf8")


def changed_files(project):
    """{đường dẫn từ gốc project: (repo, đường dẫn trong repo, commit mốc)}; None khi thiếu mốc (chạy cả assembly)."""
    path = base_path(project)
    if not path.exists():
        return None, "chưa có mốc lượt test trước"
    bases = dict(line.split("\t") for line in path.read_text(encoding="utf8").splitlines() if "\t" in line)
    subs = set(repos(project)) - {""}
    out = {}
    for rel in repos(project):
        repo = project / rel
        commit = bases.get(rel or ".")
        if not commit or git(repo, "cat-file", "-e", commit + "^{commit}")[0] != 0:
            return None, "mốc của %s không còn" % (rel or "project")
        names = git(repo, "diff", "--name-only", commit)[1].splitlines()
        names += git(repo, "ls-files", "--others", "--exclude-standard")[1].splitlines()
        for name in names:
            full = (rel + "/" + name) if rel else name
            if full not in subs:
                out[full] = (repo, name, commit)
    return out, ""


class Index:
    """Mọi .cs trong Assets và package nhúng: tên khai báo, tên được nhắc, namespace, assembly."""

    def __init__(self, project):
        self.project = project
        self.text, self.asm, self.meta_guid = {}, {}, {}
        asm_dirs, refs, auto, guid_name, self.editmode_tests = {}, {}, set(), {}, set()
        for top in ("Assets", "Packages"):
            for dp, _, fn in os.walk(project / top):
                for f in fn:
                    p = pathlib.Path(dp, f)
                    rel = p.relative_to(project).as_posix()
                    if f.endswith(".asmdef"):
                        try:
                            d = json.loads(p.read_text(encoding="utf-8-sig"))
                        except (OSError, ValueError):
                            continue
                        name = d.get("name", "")
                        asm_dirs[pathlib.PurePosixPath(rel).parent.as_posix()] = name
                        refs[name] = d.get("references", [])
                        if d.get("autoReferenced", True):
                            auto.add(name)
                        if d.get("includePlatforms") == ["Editor"] and "UNITY_INCLUDE_TESTS" in d.get("defineConstraints", []):
                            self.editmode_tests.add(name)
                        guid = self._guid(p)
                        if guid:
                            guid_name["GUID:" + guid] = name
                    elif f.endswith(".cs"):
                        try:
                            self.text[rel] = COMMENT.sub(" ", p.read_text(encoding="utf8", errors="ignore"))
                        except OSError:
                            continue
                        guid = self._guid(p)
                        if guid:
                            self.meta_guid[guid] = rel
        self.refs = {n: {guid_name.get(r, r) for r in rs} for n, rs in refs.items()}
        self.refs["Assembly-CSharp"] = set(auto)
        self.refs["Assembly-CSharp-Editor"] = set(auto) | {"Assembly-CSharp"}
        self.asm_dirs = asm_dirs
        self.decl = {p: declared(t) for p, t in self.text.items()}
        self.ns = {p: set(NAMESPACE.findall(t)) for p, t in self.text.items()}
        self.asm = {p: self._asm_of(p) for p in self.text}
        self.types = {n for d in self.decl.values() for n, receivers in d.items() if not receivers}
        # Lớp cha, interface của project mà type trong file kế thừa. Chỉ lan từ file đổi, không lan tiếp: lan ở mọi tầng thì
        # lớp nền chung (base MonoBehaviour của project) kéo cả project vào — đo: đổi một file lá chạm 327 file thay vì 32.
        self.bases = {p: {b for clause in BASES.findall(t)
                          for b in re.findall(r'([A-Za-z_]\w*)\s*(?:<[^,]*?>)?\s*(?:,|$)', clause.strip())
                          if b in self.types}
                      for p, t in self.text.items()}
        self.tokens = {p: set(IDENT.findall(t)) for p, t in self.text.items()}
        self.by_name = collections.defaultdict(set)
        for p, toks in self.tokens.items():
            for tok in toks:
                self.by_name[tok].add(p)

    def users(self, name, receivers):
        """File nhắc `name`; extension method trên type của project thì file phải nhắc cả type nhận."""
        found = self.by_name.get(name, set())
        project_receivers = receivers & self.types
        if not project_receivers:
            return found
        return {u for u in found if self.tokens[u] & project_receivers}

    @staticmethod
    def _guid(p):
        try:
            m = re.search(r"guid:\s*(\w+)", pathlib.Path(str(p) + ".meta").read_text(encoding="utf8"))
            return m.group(1) if m else None
        except OSError:
            return None

    def _asm_of(self, rel):
        d = pathlib.PurePosixPath(rel).parent.as_posix()
        while d and d != ".":
            if d in self.asm_dirs:
                return self.asm_dirs[d]
            d = pathlib.PurePosixPath(d).parent.as_posix()
        return "Assembly-CSharp-Editor" if "/Editor/" in "/" + rel else "Assembly-CSharp"

    def sees(self, user, owner, owner_ns):
        """`user` dùng được tên khai trong `owner` không: cùng assembly hoặc reference trực tiếp, rồi namespace thấy được.
        File đã xoá không còn assembly: chỉ xét namespace."""
        if owner in self.asm and self.asm[user] != self.asm[owner] \
                and self.asm[owner] not in self.refs.get(self.asm[user], ()):
            return False
        if not owner_ns:
            return True
        for n in owner_ns:
            if any(x == n or x.startswith(n + ".") for x in self.ns[user]):
                return True
            if re.search(r'(?<![\w.])' + re.escape(n) + r'\b', self.text[user]):
                return True
        return False


def declared(text):
    """Tên file khác dùng được: {tên: tập type nhận}; tập rỗng là tên type, có phần tử là extension method."""
    decls = [(len(ind.expandtabs(4)), a or b) for ind, a, b in DECL.findall(text)]
    depth = min((d for d, _ in decls), default=0)
    out = {name: set() for d, name in decls if d == depth}
    for name, receiver in EXT_METHOD.findall(text):
        out.setdefault(name, set()).add(receiver.split(".")[-1])
    return out


def yaml_scripts(project, repo, name, commit, index):
    """.cs của các object YAML có dòng đổi; file chưa track hoặc không đọc được hunk thì mọi object trong file."""
    try:
        lines = (repo / name).read_text(encoding="utf8", errors="ignore").splitlines()
    except OSError:
        return set()
    hit_lines = None
    rc, diff = git(repo, "diff", "-U0", commit, "--", name)
    if rc == 0 and diff:
        hit_lines = set()
        for m in re.finditer(r'^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@', diff, re.M):
            start, count = int(m.group(1)), int(m.group(2) or 1)
            hit_lines.update(range(start, start + max(count, 1)))
    scripts, doc_hit, doc_guids = set(), False, set()
    for i, line in enumerate(lines + ["--- !u!end"], start=1):
        if line.startswith("--- !u!"):
            if doc_hit or hit_lines is None:
                scripts |= {index.meta_guid[g] for g in doc_guids if g in index.meta_guid}
            doc_hit, doc_guids = False, set()
        if hit_lines is not None and i in hit_lines:
            doc_hit = True
        m = SCRIPT_GUID.search(line)
        if m:
            doc_guids.add(m.group(1))
    return scripts


def select(project, tests):
    changed, why = changed_files(project)
    if changed is None:
        return [("note", why + " → chạy cả assembly")] + [("all", a) for a in tests]
    global_hit = sorted(f for f in changed if GLOBAL_FILES.search(f))
    if global_hit:
        return [("note", "đổi cấu hình biên dịch (%s) → chạy cả assembly" % ", ".join(global_hit[:3]))] + [("all", a) for a in tests]
    index = Index(project)
    seeds, names = set(), collections.defaultdict(dict)
    asset_stems = set()

    def add_names(path, decl):
        for n, receivers in decl.items():
            names[path].setdefault(n, set()).update(receivers)

    for full, (repo, name, commit) in changed.items():
        if full.endswith(".cs"):
            if full in index.text:
                seeds.add(full)
            rc, old = git(repo, "show", "%s:%s" % (commit, name))
            if rc == 0:  # tên ở bản mốc: type đổi tên hay file đã xoá vẫn kéo theo chỗ còn gọi tên cũ
                add_names(full, declared(COMMENT.sub(" ", old)))
                if full not in index.text:
                    index.ns[full] = set(NAMESPACE.findall(old))
        elif full.endswith(YAML_EXT):
            seeds |= yaml_scripts(project, repo, name, commit, index)
            asset_stems.add(pathlib.PurePosixPath(full).stem)
        elif full.startswith("Assets/") and not full.endswith((".meta", ".md")):
            asset_stems.add(pathlib.PurePosixPath(full).stem)
    for p in seeds:
        add_names(p, index.decl[p])
        add_names(p, {b: set() for b in index.bases[p]})

    seen = set(seeds)
    frontier = [(p, names[p], index.ns.get(p, set())) for p in names]
    while frontier:
        nxt = []
        for owner, owner_names, owner_ns in frontier:
            for name, receivers in owner_names.items():
                for user in index.users(name, receivers):
                    if user not in seen and index.sees(user, owner, owner_ns):
                        seen.add(user)
                        nxt.append((user, index.decl[user], index.ns[user]))
        frontier = nxt
    for p, t in index.text.items():  # test đọc asset theo đường dẫn, tên file
        if index.asm[p] in index.editmode_tests and any(re.search(r'\b' + re.escape(s) + r'\b', t) for s in asset_stems):
            seen.add(p)

    by_asm = collections.defaultdict(set)
    for p in seen:
        a = index.asm[p]
        if a not in index.editmode_tests or not TEST_ATTR.search(index.text[p]):
            continue
        # Lớp fixture là lớp ngoài cùng không abstract; lớp lồng (fake, helper) không có test riêng mà vẫn tốn một lượt chạy.
        ns = sorted(index.ns[p])
        outer = [(len(m.group(1).expandtabs(4)), m.group(2)) for m in TOP_CLASS.finditer(index.text[p])]
        depth = min((d for d, _ in outer), default=0)
        abstract = set(ABSTRACT.findall(index.text[p]))
        for d, cls in outer:
            if d == depth and cls not in abstract:
                by_asm[a].add((ns[0] + "." if ns else "") + cls)
    # Fail mới chưa qua lại (dòng "+" verify.sh ghi) luôn chạy lại: code có thể đã trả về đúng bản mốc, diff rỗng, mà
    # nhãn fail mới vẫn treo.
    pending = 0
    for fail_file in (project / "Library" / "HorcruxVerify").glob("*.fail"):
        for line in fail_file.read_text(encoding="utf8").splitlines()[1:]:
            if line.startswith("+"):
                by_asm[fail_file.stem].add(line[1:].split("(")[0].rsplit(".", 1)[0])
                pending += 1
    out = [("note", "%d file đổi từ mốc, %d file code bị chạm%s" % (
        len(changed), len(seen), ", %d fail mới chạy lại" % pending if pending else ""))]
    extra = sorted(set(by_asm) - set(tests))
    if extra:
        out.append(("note", "tự thêm assembly test bị chạm: " + ", ".join(extra)))
    for a in list(tests) + extra:
        classes = sorted(by_asm.get(a, ()))
        if not classes:
            continue
        if len(classes) > MAX_CLASS_RUNS:
            out.append(("all", a))
        else:
            out += [("class", a, c) for c in classes]
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--project", default=".")
    ap.add_argument("--tests", default="")
    ap.add_argument("--record", action="store_true")
    args = ap.parse_args()
    project = pathlib.Path(args.project).resolve()
    if args.record:
        record(project)
        return
    for row in select(project, [t for t in args.tests.split(",") if t]):
        print("\t".join(row))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
