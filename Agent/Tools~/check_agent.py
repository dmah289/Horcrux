"""Kiểm bộ Agent/ của Horcrux: con trỏ, định nghĩa, trần kích thước, mục developer-only, nội dung so với mốc.
`all` còn in cảnh báo `CÓ THỂ ĐÃ NÂNG` (không tính lỗi) cho khối memory mà câu Làm phần lớn đã có ở luật, tra cứu hay tài
liệu công cụ cùng miền — khối nâng rồi mà chưa xoá; đối chiếu tay rồi `memory drop`.

    python -X utf8 -I check_agent.py all [--base <git-ref>]
    python -X utf8 -I check_agent.py preserve <git-ref>
    python -X utf8 -I check_agent.py meta
    python -X utf8 -I check_agent.py memory drop <file> "<đầu tiêu đề>" ... [--dry-run]
    python -X utf8 -I check_agent.py memory move <từ> <tới> "<đầu tiêu đề>" ... [--dry-run]

`memory` dọn file memory theo khối `### `: <file> là tên trong `Agent/Memory/` (`workflow`, `inbox.md`) hoặc đường dẫn
(memory dự án `docs/agent-memory/x.md`); mỗi đầu tiêu đề phải khớp đúng một khối. Giữ kiểu xuống dòng của từng file. Từ
chối, không ghi gì, khi: `drop` mà tiêu đề còn được nhắc ở file khác · `move` mà dòng nhắc tiêu đề còn gọi tên file nguồn ·
file đích đã có khối cùng tiêu đề hay sẽ vượt trần memory.
"""
import pathlib
import re
import subprocess
import sys
import uuid

AGENT = pathlib.Path(__file__).resolve().parent.parent
HORCRUX = AGENT.parent
RULES = ["workflow", "reuse-gate", "code-design", "async-presentation", "editor-first", "math-physics", "docs"]
SKILLS = ["feel-tune", "build-size", "compile-check", "ui-reskin", "demo-compare", "merge-audit"]
ALWAYS = ["workflow", "reuse-gate"]
CEIL = {"horcrux": 26 * 1024, "rule": 13 * 1024, "memory": 8 * 1024}
DEV_START, DEV_END = "<!-- DEVELOPER-ONLY: START", "<!-- DEVELOPER-ONLY: END -->"
LABEL = re.compile(
    r"^`\d{4}-\d{2}` · vấp \d+ · (đã đo|đọc tài liệu|suy luận|chuyển sang, chưa kiểm lại)( · đúng với .+)?$")
POINTER = re.compile(r"`([a-z-]+)` › (tra cứu › )?(`?[^\s,.;:)·|(]+(?: [^\s,.;:)·|(]+)?)")


def read(p):
    return p.read_text(encoding="utf-8")


def headings(p):
    return [l.lstrip("#").strip() for l in read(p).splitlines() if l.startswith("#")]


def main_file(name):
    return AGENT / "Rules" / name / "RULE.md" if name in RULES else AGENT / "Skills" / name / "SKILL.md"


def ref_file(name):
    return main_file(name).with_name("reference.md")


def all_files():
    out = [AGENT / "horcrux.md"]
    for n in RULES + SKILLS:
        out.append(main_file(n))
        if ref_file(n).exists():
            out.append(ref_file(n))
    out += sorted((AGENT / "Memory").glob("*.md")) if (AGENT / "Memory").exists() else []
    return out


def check_pointers():
    bad = 0
    for f in all_files():
        for i, line in enumerate(read(f).splitlines(), 1):
            for m in POINTER.finditer(line):
                name, ref, sec = m.group(1), m.group(2), m.group(3).strip("`")
                if name not in RULES + SKILLS:
                    continue
                target = ref_file(name) if ref else main_file(name)
                if not target.exists():
                    print("KHÔNG CÓ FILE", f.relative_to(AGENT), i, m.group(0)); bad += 1; continue
                first = sec.split(" ")[0]
                if not any(h.startswith(sec) or h.startswith(first) for h in headings(target)):
                    print("KHÔNG CÓ MỤC", f.relative_to(AGENT), i, m.group(0)); bad += 1
    for n in RULES + SKILLS:
        ref = ref_file(n)
        text = read(main_file(n))
        if not ref.exists():
            continue
        if "## Tra cứu" not in text:
            print("THIẾU BẢNG TRA CỨU", n); bad += 1; continue
        hs = [h.replace("`", "") for h in headings(ref)]
        for row in text.split("## Tra cứu")[1].splitlines():
            cells = [c.strip() for c in row.strip().strip("|").split("|")]
            if len(cells) == 2 and cells[1] != "Mục" and not set(cells[1]) <= set("-"):
                if cells[1].replace("`", "") not in hs:
                    print("TRA CỨU TRỎ HỤT", n, cells[1]); bad += 1
    return bad


def check_definitions():
    bad = 0
    for f in all_files():
        rel = f.relative_to(AGENT)
        text = read(f)
        if rel.parts[0] in ("Rules", "Skills") and ("*Bẫy:*" in text or re.search(r"^## Bẫy", text, re.M)):
            print("CÒN BẪY NGOÀI MEMORY", rel); bad += 1
        # inbox.md cũng kiểm: khối chờ xếp miền vẫn phải đúng hình dạng, không thì lỗi chỉ lộ lúc dời về miền.
        if rel.parts[0] == "Memory":
            lines = text.splitlines()
            for i, line in enumerate(lines):
                if not line.startswith("### "):
                    continue
                label = lines[i + 1] if i + 1 < len(lines) else ""
                body = "\n".join(lines[i + 2:i + 8])
                if not LABEL.match(label):
                    print("SAI DÒNG NHÃN", rel, i + 2, label[:60]); bad += 1
                if not all(k in body for k in ("**Thấy:**", "**Vì:**", "**Làm:**")):
                    print("THIẾU THẤY/VÌ/LÀM", rel, i + 1, line[:60]); bad += 1
    return bad


# Phần cụm ba từ của câu Làm đã có ở luật, tra cứu hay tài liệu công cụ. Đo trên đợt dọn 2026-11 (73 khối): ngưỡng 0.3
# bắt 31/39 khối đã nâng, báo 2/34 khối giữ (cả hai có câu Làm nằm trong luật); so nguyên văn từng vế chỉ bắt 15/39.
PROMOTED_SHARE = 0.3
GRAM = 3


def flat(text):
    """Chữ thường, bỏ con trỏ `(… › …)`, dấu câu và định dạng — để so vế câu không lệch vì backtick, xuống dòng."""
    text = re.sub(r"\([^()]*›[^()]*\)", " ", text)
    text = re.sub(r"[^\w\s]", " ", text.replace("`", "").replace("*", "").lower())
    return re.sub(r"\s+", " ", text).strip()


def grams(text):
    words = text.split()
    return {" ".join(words[i:i + GRAM]) for i in range(len(words) - GRAM + 1)}


def promoted_corpus(name):
    """Cụm từ của luật, tra cứu và tài liệu công cụ mà luật hay skill `name` gọi tên — nơi bài học đã nâng sẽ nằm."""
    files = [main_file(name), ref_file(name)]
    main = read(main_file(name)) if main_file(name).exists() else ""
    files += [HORCRUX / p for p in re.findall(r"`(docs/[^`\s]+\.md)`", main)]
    return grams(" ".join(flat(read(f)) for f in files if f.exists()))


def promoted_share(lam, corpus):
    """Cụm từ, không vế nguyên văn: luật thường viết lại bài học bằng lời khác."""
    own = grams(flat(lam.replace("**Làm:**", "")))
    return len(own & corpus) / len(own) if own else 0.0


def check_promoted():
    """Cảnh báo, không tính lỗi: khối memory mà phần lớn câu Làm đã có nguyên văn ở luật, tra cứu hay tài liệu công cụ —
    thường là khối nâng rồi mà chưa xoá. Đối chiếu tay rồi `memory drop`."""
    for name in RULES + SKILLS:
        mem = AGENT / "Memory" / f"{name}.md"
        if not mem.exists():
            continue
        corpus = promoted_corpus(name)
        lines = read(mem).splitlines()
        for i, line in enumerate(lines):
            if not line.startswith("### "):
                continue
            lam = next((l for l in lines[i + 1:i + 8] if l.startswith("**Làm:**")), "")
            share = promoted_share(lam, corpus)
            if share >= PROMOTED_SHARE:
                print(f"CÓ THỂ ĐÃ NÂNG Memory\\{mem.name} {i + 1} ({share:.0%} câu Làm đã có ở luật, tra cứu): {line[4:64]}")


def check_sizes():
    bad, always = 0, 0
    for f in all_files():
        rel, size = f.relative_to(AGENT), len(f.read_bytes())
        kind = "horcrux" if rel.name == "horcrux.md" else "rule" if rel.name == "RULE.md" else \
            "memory" if rel.parts[0] == "Memory" else None
        first = rel.name == "horcrux.md" or (len(rel.parts) > 1 and rel.parts[1] in ALWAYS and rel.name == "RULE.md") \
            or (rel.parts[0] == "Memory" and rel.stem in ALWAYS)
        always += size if first else 0
        over = kind and size > CEIL[kind]
        bad += 1 if over else 0
        print(f"{size:7d} {'ĐẦU PHIÊN' if first else '         '} {'VƯỢT TRẦN' if over else ''} {rel}")
    print("đọc đầu phiên:", always)
    return bad


def git_show(ref, path):
    r = subprocess.run(["git", "-C", str(HORCRUX), "show", f"{ref}:{path}"], capture_output=True)
    return r.stdout.decode("utf-8") if r.returncode == 0 else None


def dev_block(text):
    if not text or DEV_START not in text or DEV_END not in text:
        return None
    return text[text.index(DEV_START):text.index(DEV_END) + len(DEV_END)]


def check_dev_block(ref):
    old = dev_block(git_show(ref, "Skills/horcrux_skill.md") or git_show(ref, "Agent/horcrux.md"))
    new = dev_block(read(AGENT / "horcrux.md"))
    if old is None or new is None:
        print("mục developer-only: THIẾU DẤU", "ở mốc" if old is None else "ở horcrux.md")
        return 1
    same = old == new
    print("mục developer-only:", "nguyên" if same else "ĐÃ BỊ ĐỔI")
    return 0 if same else 1


def norm(line):
    return re.sub(r"\s+", " ", line.strip().lstrip("-*> ").strip())


def preserve(ref):
    """In các dòng có ở mốc mà không còn ở đâu trong Agent/ — để soát tay, không tự kết luận là mất."""
    new = " ".join(norm(l) for f in all_files() for l in read(f).splitlines())
    names = subprocess.run(["git", "-C", str(HORCRUX), "ls-tree", "-r", "--name-only", ref, "Skills", "Agent"],
                           capture_output=True).stdout.decode("utf-8").splitlines()
    missing = 0
    for path in names:
        if not path.endswith(".md"):
            continue
        for i, line in enumerate(git_show(ref, path).splitlines(), 1):
            n = norm(line)
            if len(n) > 12 and n not in new:
                print(f"{path}:{i}: {n[:140]}"); missing += 1
    print("dòng không tìm thấy:", missing)


def write_meta():
    """Sinh .meta còn thiếu cho thư mục và file .md dưới Agent/ (khi Unity đang đóng)."""
    folder_tmpl = read(AGENT / "Rules" / "workflow.meta")
    file_tmpl = read(AGENT / "horcrux.md.meta")
    for p in sorted(AGENT.rglob("*")):
        if any(part.endswith("~") for part in p.relative_to(AGENT).parts):
            continue
        if not (p.is_dir() or p.suffix == ".md"):
            continue
        meta = p.with_name(p.name + ".meta")
        if not meta.exists():
            tmpl = folder_tmpl if p.is_dir() else file_tmpl
            meta.write_bytes(re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, tmpl).encode("utf-8"))
            print("đã sinh", meta.relative_to(AGENT))


# ---------- dọn memory ----------

PROJECT = HORCRUX.parent.parent if HORCRUX.parent.name == "Assets" else None


class MemoryFile:
    """File memory tách thành phần đầu và các khối `### `; ghi lại giữ kiểu xuống dòng gốc."""

    def __init__(self, arg):
        named = AGENT / "Memory" / (arg if arg.endswith(".md") else arg + ".md")
        self.path = named if "/" not in arg and "\\" not in arg else pathlib.Path(arg).resolve()
        raw = self.path.read_bytes()
        self.eol = "\r\n" if b"\r\n" in raw else "\n"
        self.head, *self.blocks = re.split(r"(?m)^(?=### )", raw.decode("utf-8").replace("\r\n", "\n"))

    @staticmethod
    def title(block):
        return block.split("\n", 1)[0][4:].strip()

    def pick(self, prefixes):
        """Khối khớp mỗi đầu tiêu đề; lỗi khi một đầu khớp không hoặc nhiều khối."""
        chosen, errors = [], []
        for prefix in prefixes:
            hits = [b for b in self.blocks if self.title(b).startswith(prefix)]
            if len(hits) != 1:
                errors.append(f"{self.path.name}: \"{prefix}\" khớp {len(hits)} khối, cần đúng 1")
            elif hits[0] not in chosen:
                chosen.append(hits[0])
        return chosen, errors

    def text(self, blocks):
        body = self.head + "".join(b.rstrip("\n") + "\n\n" for b in blocks)
        return (body.rstrip("\n") + "\n").replace("\n", self.eol).encode("utf-8")


def mentions(title, skip):
    """(file, dòng, nội dung) nhắc tiêu đề, ngoài file `skip`: Agent/, tài liệu Horcrux, tài liệu và memory dự án.
    So theo vế trước dấu phẩy đầu tiên: con trỏ hay trích phần đầu tiêu đề. Dòng `### ` là khối, không phải con trỏ."""
    key = title.split(",")[0].strip()
    key = key if len(key) >= 12 else title
    roots = [HORCRUX] + ([PROJECT / "docs", PROJECT / "Assets", PROJECT / "CLAUDE.md"] if PROJECT else [])
    seen, out = set(), []
    for root in roots:
        files = [root] if root.is_file() else root.rglob("*.md") if root.exists() else []
        for f in files:
            if f in seen or f.resolve() == skip.resolve():
                continue
            seen.add(f)
            for i, line in enumerate(f.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
                if key in line and not line.startswith("### "):
                    out.append((f, i, line.strip()))
    return out


def memory(args):
    dry = "--dry-run" in args
    args = [a for a in args if a != "--dry-run"]
    if len(args) < 3 or args[0] not in ("drop", "move") or (args[0] == "move" and len(args) < 4):
        print(__doc__)
        return 2
    action, source = args[0], MemoryFile(args[1])
    target = MemoryFile(args[2]) if action == "move" else None
    if target and target.path.resolve() == source.path.resolve():
        print("memory move: file nguồn và đích trùng nhau")
        return 2
    chosen, errors = source.pick(args[3:] if target else args[2:])
    for block in chosen:
        title = MemoryFile.title(block)
        for f, i, line in mentions(title, source.path):
            if target and source.path.name not in line:
                continue
            where = f"{f.relative_to(PROJECT or HORCRUX).as_posix()}:{i}"
            errors.append(f"còn con trỏ tới \"{title}\" ở {where} — sửa con trỏ trước" if target else
                          f"\"{title}\" còn được nhắc ở {where} — sửa hay bỏ con trỏ trước khi xoá")
        if target and any(MemoryFile.title(b) == title for b in target.blocks):
            errors.append(f"{target.path.name} đã có khối \"{title}\"")
    kept = [b for b in source.blocks if b not in chosen]
    new_source = source.text(kept)
    new_target = target.text(target.blocks + chosen) if target else None
    if target and len(new_target) > CEIL["memory"]:
        errors.append(f"{target.path.name} sẽ {len(new_target)} byte, vượt trần {CEIL['memory']} — dọn file đích trước")
    if errors:
        print("\n".join(errors))
        print("memory: không ghi gì")
        return 1
    for block in chosen:
        verb = f"dời {source.path.name} → {target.path.name}" if target else f"xoá {source.path.name}"
        print(f"{verb}: {MemoryFile.title(block)}")
    print(f"{source.path.name}: {len(source.path.read_bytes())} → {len(new_source)} byte" +
          (f" · {target.path.name}: {len(target.path.read_bytes())} → {len(new_target)} byte" if target else ""))
    if dry:
        print("memory: --dry-run, không ghi gì")
        return 0
    source.path.write_bytes(new_source)
    if target:
        target.path.write_bytes(new_target)
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")  # console Windows mặc định cp1252, không in được tiếng Việt
    cmd = sys.argv[1] if len(sys.argv) > 1 else "all"
    if cmd == "memory":
        sys.exit(memory(sys.argv[2:]))
    if cmd == "preserve":
        preserve(sys.argv[2])
    elif cmd == "meta":
        write_meta()
    else:
        bad = check_pointers() + check_definitions() + check_sizes()
        check_promoted()
        if "--base" in sys.argv:
            bad += check_dev_block(sys.argv[sys.argv.index("--base") + 1])
        print("lỗi:", bad)
        sys.exit(1 if bad else 0)
