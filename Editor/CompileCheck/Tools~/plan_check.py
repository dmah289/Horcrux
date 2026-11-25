#!/usr/bin/env python3
"""Kiểm code trong một Plan trước khi giao: mỗi khối ```csharp khớp nguyên văn một file mẫu, file mẫu build được trong
assembly sẽ dùng nó. In gọn, mã thoát 1 khi một khối lệch hoặc build hỏng.

Dùng:
  python -X utf8 -I plan_check.py --plan <Plan.md> [--project <gốc project>] [--csproj <A.csproj>]
         [--sample <file mẫu.cs> ...] [--replace <file trong csproj>=<file mẫu.cs> ...] [--match <file> ...]

  --plan     Plan cần kiểm; mọi khối ```csharp / ```cs phải nằm nguyên văn trong một file mẫu hoặc file --match.
  --sample   file mẫu viết ở scratchpad: được so khối và được thêm vào csproj khi build.
  --replace  thay một file đang có trong csproj (đường dẫn như trong csproj, `/` hay `\\` đều được) bằng file mẫu — bản tạm
             của dự án đổi sang bản Plan. File mẫu bên phải cũng được so khối.
  --match    file chỉ để so khối, không build — file thật chứa "đoạn cũ" của chỗ Plan viết cũ → mới.
  --csproj   csproj do Unity sinh ở gốc project, của assembly sẽ gọi code Plan; bỏ trống thì chỉ so khối.
  --project  gốc project; mặc định thư mục hiện tại.

Build chạy trên bản chép csproj đặt cạnh bản gốc (đường dẫn tương đối vẫn đúng), output vào Temp/PlanCheck/, không build
lại project tham chiếu — dll của chúng phải có sẵn: chạy verify.sh một lần trước. Builder lấy từ `verify.sh --print-builder`.
"""
import argparse
import pathlib
import re
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
CODE_FENCE = re.compile(r"^```(?:csharp|cs)\s*$")
MAX_ERRORS = 40


def windows_path(raw):
    """Đường dẫn Git Bash /d/x → D:\\x; MSYS không đổi đối số nằm sau dấu '=' của --replace."""
    m = re.match(r"^/([a-zA-Z])/(.*)$", raw)
    if m:
        raw = f"{m.group(1).upper()}:/{m.group(2)}"
    return pathlib.Path(raw).resolve()


def normalized(text):
    return text.replace("\r\n", "\n").replace("\r", "\n")


def plan_blocks(plan_text):
    """(dòng mở fence, nội dung) của mọi khối C# trong Plan."""
    blocks, current, start = [], None, 0
    for number, line in enumerate(normalized(plan_text).split("\n"), 1):
        if current is None:
            if CODE_FENCE.match(line):
                current, start = [], number
        elif line.startswith("```"):
            blocks.append((start, "\n".join(current)))
            current = None
        else:
            current.append(line)
    return blocks


def closest_mismatch(block, candidates):
    """Chỗ lệch đầu tiên so với file khớp được nhiều dòng liên tiếp nhất, tính từ dòng đầu của khối."""
    block_lines = block.split("\n")
    best = None
    for name, text in candidates.items():
        lines = text.split("\n")
        for start, line in enumerate(lines):
            if line != block_lines[0]:
                continue
            matched = 0
            while (matched < len(block_lines) and start + matched < len(lines)
                   and lines[start + matched] == block_lines[matched]):
                matched += 1
            if best is None or matched > best[0]:
                sample_line = lines[start + matched] if start + matched < len(lines) else "<hết file>"
                best = (matched, name, start + matched + 1, block_lines[matched], sample_line)
    return best


def check_blocks(plan_path, candidates):
    blocks = plan_blocks(plan_path.read_text(encoding="utf-8-sig"))
    if not blocks:
        print(f"so khối: {plan_path.name} không có khối ```csharp nào")
        return False
    ok = True
    for fence_line, block in blocks:
        owner = next((name for name, text in candidates.items() if block in text), None)
        if owner:
            print(f"khối dòng {fence_line}: khớp {owner}")
            continue
        ok = False
        miss = closest_mismatch(block, candidates)
        if miss is None:
            print(f"khối dòng {fence_line}: LỆCH — dòng đầu không có trong file mẫu nào: {block.split(chr(10))[0]!r}")
        else:
            matched, name, sample_line_no, plan_line, sample_line = miss
            print(f"khối dòng {fence_line}: LỆCH — {name} khớp {matched} dòng đầu, lệch ở dòng {sample_line_no} của file mẫu")
            print(f"  Plan: {plan_line!r}")
            print(f"  mẫu : {sample_line!r}")
    return ok


def builder():
    result = subprocess.run(["bash", str(HERE / "verify.sh"), "--print-builder"], capture_output=True, text=True,
                            encoding="utf-8")
    out = result.stdout.strip()
    if result.returncode != 0 or not out:
        print(out or result.stderr.strip() or "build: verify.sh --print-builder không trả builder")
        return None
    return out


def patched_csproj(csproj_text, project, samples, replacements):
    """Bản chép csproj: file mẫu vào, file bị thay ra, output về Temp/PlanCheck/. Trả (text, lỗi)."""
    text = csproj_text
    for old, new in replacements:
        key = old.replace("/", "\\")
        include = f'<Compile Include="{key}" />'
        if include not in text:
            return None, f"--replace: csproj không có {include}"
        text = text.replace(include, f'<Compile Include="{new}" />')
    added = "".join(f'<Compile Include="{sample}" />\n    ' for sample in samples)
    first_compile = text.find("<Compile Include=")
    if first_compile < 0:
        return None, "csproj không có <Compile Include> nào"
    text = text[:first_compile] + added + text[first_compile:]
    out_dir = project / "Temp" / "PlanCheck"
    # Lambda, không chuỗi thay thế: đường dẫn Windows có '\U', '\b' mà re.sub đọc thành escape.
    text, count = re.subn(r"<OutputPath>[^<]*</OutputPath>",
                          lambda _: f"<OutputPath>{out_dir}\\bin\\</OutputPath>"
                                    f"\n    <IntermediateOutputPath>{out_dir}\\obj\\</IntermediateOutputPath>", text)
    if count != 1:
        return None, f"csproj có {count} thẻ <OutputPath>, cần đúng 1"
    return text, None


def build(project, csproj, samples, replacements):
    source = project / csproj
    if not source.is_file():
        print(f"build {csproj}: không có file (Unity chưa sinh csproj?)")
        return False
    text, error = patched_csproj(source.read_text(encoding="utf-8-sig"), project, samples, replacements)
    if error:
        print(f"build {csproj}: {error}")
        return False
    tool = builder()
    if tool is None:
        return False
    copy = source.with_name(f"PlanCheck.{source.name}")
    copy.write_text(text, encoding="utf-8")
    try:
        common = ["-p:Configuration=Debug", "-p:BuildProjectReferences=false"]
        if tool == "dotnet":
            command = ["dotnet", "build", str(copy), "-nologo", "-v", "q", "-clp:ErrorsOnly", *common]
        else:
            command = [tool, str(copy), "-t:Build", "-v:q", "-nologo", "-clp:ErrorsOnly", *common]
        result = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace")
    finally:
        copy.unlink(missing_ok=True)
    output = (result.stdout + result.stderr).replace("\r", "")
    errors = sorted({re.sub(r"\s*\[[^\]]*csproj\]\s*$", "", line).strip()
                     for line in output.split("\n") if re.search(r"error (CS|MSB)\d+", line)})
    if errors:
        print(f"build {csproj} + file mẫu: LỖI")
        print("\n".join(errors[:MAX_ERRORS]))
        if any("CS0006" in line for line in errors):
            print("  CS0006: dll của project tham chiếu chưa có — chạy verify.sh build csproj này một lần rồi chạy lại")
        return False
    if result.returncode != 0:
        print(f"build {csproj} + file mẫu: LỖI (mã thoát {result.returncode}, không có dòng error CS/MSB)")
        print("\n".join(output.strip().split("\n")[:20]))
        return False
    print(f"build {csproj} + file mẫu: ok")
    return True


def main():
    parser = argparse.ArgumentParser(add_help=True)
    parser.add_argument("--plan", required=True)
    parser.add_argument("--project", default=".")
    parser.add_argument("--csproj")
    parser.add_argument("--sample", action="append", default=[])
    parser.add_argument("--replace", action="append", default=[])
    parser.add_argument("--match", action="append", default=[])
    args = parser.parse_args()

    project = windows_path(args.project)
    plan = windows_path(args.plan)
    samples = [windows_path(path) for path in args.sample]
    replacements = []
    for pair in args.replace:
        old, sep, new = pair.partition("=")
        if not sep:
            print(f"--replace cần dạng <file trong csproj>=<file mẫu>: {pair}")
            return 2
        replacements.append((old.strip(), windows_path(new.strip())))
    matched_only = [windows_path(path) for path in args.match]

    # File vừa thay vừa khai --sample thì chỉ vào csproj một lần.
    samples = [path for path in samples if path not in {new for _, new in replacements}]
    files = samples + [new for _, new in replacements] + matched_only
    missing = [str(path) for path in [plan, *files] if not path.is_file()]
    if missing:
        print("không có file: " + ", ".join(missing))
        return 2
    # Khoá kèm thư mục cha: bản mẫu và bản thật thường trùng tên file.
    candidates = {f"{path.parent.name}/{path.name}": normalized(path.read_text(encoding="utf-8-sig")) for path in files}
    if not candidates:
        print("cần ít nhất một --sample, --replace hoặc --match để so khối")
        return 2

    failed = []
    if not check_blocks(plan, candidates):
        failed.append("khối")
    if args.csproj and not build(project, args.csproj, samples, replacements):
        failed.append("build")
    print(f"plan_check: FAIL {' '.join(failed)}" if failed else "plan_check: OK")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
