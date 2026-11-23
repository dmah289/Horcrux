"""Kiểm bộ Agent/ của Horcrux: con trỏ, định nghĩa, trần kích thước, mục developer-only, nội dung so với mốc.

    python -X utf8 -I check_agent.py all [--base <git-ref>]
    python -X utf8 -I check_agent.py preserve <git-ref>
    python -X utf8 -I check_agent.py meta
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


def all_files():
    out = [AGENT / "horcrux.md"]
    for n in RULES:
        out.append(main_file(n))
        if (AGENT / "Rules" / n / "reference.md").exists():
            out.append(AGENT / "Rules" / n / "reference.md")
    out += [main_file(n) for n in SKILLS]
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
                target = AGENT / "Rules" / name / "reference.md" if ref else main_file(name)
                if not target.exists():
                    print("KHÔNG CÓ FILE", f.relative_to(AGENT), i, m.group(0)); bad += 1; continue
                first = sec.split(" ")[0]
                if not any(h.startswith(sec) or h.startswith(first) for h in headings(target)):
                    print("KHÔNG CÓ MỤC", f.relative_to(AGENT), i, m.group(0)); bad += 1
    for n in RULES:
        ref = AGENT / "Rules" / n / "reference.md"
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
        if rel.parts[0] == "Memory" and rel.name != "inbox.md":
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
    return text[text.index(DEV_START):text.index(DEV_END) + len(DEV_END)]


def check_dev_block(ref):
    old = git_show(ref, "Skills/horcrux_skill.md") or git_show(ref, "Agent/horcrux.md")
    same = dev_block(old) == dev_block(read(AGENT / "horcrux.md"))
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


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "all"
    if cmd == "preserve":
        preserve(sys.argv[2])
    elif cmd == "meta":
        write_meta()
    else:
        bad = check_pointers() + check_definitions() + check_sizes()
        if "--base" in sys.argv:
            bad += check_dev_block(sys.argv[sys.argv.index("--base") + 1])
        print("lỗi:", bad)
        sys.exit(1 if bad else 0)
