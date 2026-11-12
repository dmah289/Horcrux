"""Soát một .unitypackage trước khi import: thứ gốc cần kéo theo, phần còn lại so với dự án. Xem docs/Editor/UnityPackageAudit.md.

  unitypackage_audit.py <gói.unitypackage> [--project <thư mục dự án>] [--root <chuỗi trong đường dẫn> …] [--list]
  unitypackage_audit.py selftest

Đọc gói ngay trong bộ nhớ (tar.gz), không giải nén ra đĩa. Chỉ cần thư viện chuẩn của Python.
"""
import argparse
import io
import os
import re
import sys
import tarfile
from collections import OrderedDict, defaultdict

GUID_REF = re.compile(rb'guid: ([0-9a-f]{32})')
META_GUID = re.compile(r'^guid: ([0-9a-f]{32})', re.M)
BUILTIN_PREFIX = '0000000000000000'   # guid của tài nguyên dựng sẵn trong Unity (default resources, builtin extra)
ROOT_EXTS = ('.prefab', '.unity')     # mặc định gốc là mọi prefab, scene trong gói: thứ người import thường cần đặt vào màn


class Entry:
    __slots__ = ('guid', 'path', 'asset', 'meta', 'refs', 'binary')

    def __init__(self, guid):
        self.guid = guid
        self.path = None
        self.asset = None      # bytes; None là thư mục
        self.meta = b''
        self.refs = set()
        self.binary = False

    @property
    def is_dir(self):
        return self.asset is None

    @property
    def size(self):
        return len(self.asset) if self.asset is not None else 0


def read_package(path):
    """{guid: Entry} theo thứ tự đường dẫn. Mỗi guid là một thư mục trong tar: pathname, asset (không có nếu là thư mục), asset.meta."""
    entries = {}
    try:
        tar = tarfile.open(path, 'r:gz')
    except (OSError, tarfile.TarError) as e:
        sys.exit('unitypackage_audit: không đọc được %s (%s)' % (path, e))
    with tar:
        for member in tar.getmembers():
            if not member.isfile():
                continue
            parts = member.name.replace('\\', '/').strip('./').split('/')
            if len(parts) != 2:
                continue
            guid, name = parts
            if name not in ('pathname', 'asset', 'asset.meta'):
                continue
            data = tar.extractfile(member).read()
            e = entries.setdefault(guid, Entry(guid))
            if name == 'pathname':
                e.path = data.decode('utf-8', 'replace').splitlines()[0].strip()
            elif name == 'asset':
                e.asset = data
            else:
                e.meta = data
    for e in entries.values():
        if e.path is None:
            e.path = '(không tên) ' + e.guid
        if e.asset is not None:
            if e.asset[:5] == b'%YAML':
                e.refs |= {g.decode() for g in GUID_REF.findall(e.asset)}
            elif e.path.lower().endswith(ROOT_EXTS + ('.mat', '.asset', '.controller', '.anim', '.overridecontroller')):
                e.binary = True
        # importer của model, sprite atlas… trỏ asset khác trong .meta (externalObjects)
        e.refs |= {g.decode() for g in GUID_REF.findall(e.meta)} - {e.guid}
        e.refs.discard(e.guid)
    return OrderedDict(sorted(entries.items(), key=lambda kv: kv[1].path.lower()))


def index_project(root):
    """(guid → đường dẫn tương đối, tập đường dẫn) từ mọi .meta trong Assets/ và Packages/ của dự án."""
    by_guid = {}
    paths = set()
    for top in ('Assets', 'Packages'):
        base = os.path.join(root, top)
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [d for d in dirnames if not d.endswith('~') and not d.startswith('.')]
            for f in filenames:
                if not f.endswith('.meta'):
                    continue
                full = os.path.join(dirpath, f)
                rel = os.path.relpath(full[:-5], root).replace('\\', '/')
                paths.add(rel)
                try:
                    with open(full, encoding='utf-8', errors='replace') as fh:
                        m = META_GUID.search(fh.read(400))
                except OSError:
                    continue
                if m:
                    by_guid[m.group(1)] = rel
    return by_guid, paths


def same_bytes(a, b):
    return a.replace(b'\r\n', b'\n') == b.replace(b'\r\n', b'\n')


def classify(e, project, by_guid, paths):
    """(nhãn, đường dẫn trong dự án nếu khác đường dẫn trong gói). Nhãn: mới · giống · khác bản · trùng đường dẫn khác guid.
    Unity import theo guid: asset đã có guid đó được ghi đè ở chỗ của nó trong dự án, dù gói đặt ở đường dẫn khác."""
    if project is None:
        return 'mới', None
    where = by_guid.get(e.guid)
    if where is None:
        return ('trùng đường dẫn khác guid', e.path) if e.path in paths else ('mới', None)
    moved = where if where != e.path else None
    full = os.path.join(project, where)
    if e.is_dir or os.path.isdir(full):
        return 'giống', moved
    try:
        with open(full, 'rb') as fh:
            ours = fh.read()
    except OSError:
        return 'khác bản', moved
    return ('giống' if same_bytes(ours, e.asset) else 'khác bản'), moved


def closure(entries, roots):
    """Mọi guid trong gói mà gốc kéo theo (gồm gốc), và tập guid được trỏ tới nhưng không có trong gói."""
    need, outside = set(), set()
    stack = list(roots)
    while stack:
        g = stack.pop()
        if g in need:
            continue
        need.add(g)
        for r in entries[g].refs:
            if r in entries:
                stack.append(r)
            else:
                outside.add(r)
    # thư mục cha của file cần cũng phải có khi import
    dirs = {e.path: g for g, e in entries.items() if e.is_dir}
    for g in list(need):
        p = entries[g].path
        while '/' in p:
            p = p.rsplit('/', 1)[0]
            if p in dirs:
                need.add(dirs[p])
    return need, outside


def human(n):
    for unit in ('B', 'KB', 'MB', 'GB'):
        if n < 1024 or unit == 'GB':
            return ('%d %s' % (n, unit)) if unit == 'B' else ('%.1f %s' % (n, unit))
        n /= 1024.0


def group_key(path):
    parts = path.split('/')
    return '/'.join(parts[:2]) + '/' if len(parts) > 2 else path


def audit(package, project, root_filters, list_all, out=print):
    entries = read_package(package)
    by_guid, paths = index_project(project) if project else ({}, set())
    files = [e for e in entries.values() if not e.is_dir]
    out('gói: %s · %d mục (%d thư mục, %d file, %s)' % (
        os.path.basename(package), len(entries), len(entries) - len(files), len(files), human(sum(e.size for e in files))))
    if project:
        out('dự án: %s · %d asset có .meta' % (project, len(by_guid)))
    else:
        out('dự án: không đưa --project, mọi mục coi là mới')

    if root_filters:
        roots = [g for g, e in entries.items() if not e.is_dir and any(f.lower() in e.path.lower() for f in root_filters)]
        if not roots:
            sys.exit('unitypackage_audit: không mục nào khớp --root %s' % ', '.join(root_filters))
    else:
        roots = [g for g, e in entries.items() if e.path.lower().endswith(ROOT_EXTS)]
    out('gốc (%d): %s' % (len(roots), ', '.join(entries[g].path for g in roots) if roots else 'không có prefab, scene; đặt --root'))

    status = {g: classify(e, project, by_guid, paths) for g, e in entries.items()}
    need, outside = closure(entries, roots)
    need_files = [entries[g] for g in entries if g in need and not entries[g].is_dir]
    out('')
    out('cần cho gốc: %d file, %s — tick đúng các dòng này khi import' % (len(need_files), human(sum(e.size for e in need_files))))
    for e in need_files:
        st, where = status[e.guid]
        tail = (' → dự án đặt ở %s' % where) if where else ''
        flag = '  nhị phân, không đọc được tham chiếu' if e.binary else ''
        out('  %-26s %9s  %s%s%s' % (st, human(e.size), e.path, tail, flag))

    builtin = {g for g in outside if g.startswith(BUILTIN_PREFIX)}
    in_project = {g for g in outside - builtin if g in by_guid}
    missing = outside - builtin - in_project
    out('gốc trỏ ra ngoài gói: dự án đã có %d · dựng sẵn của Unity %d · không thấy ở đâu %d' % (len(in_project), len(builtin), len(missing)))
    for g in sorted(in_project, key=lambda g: by_guid[g]):
        out('  dự án có  %s' % by_guid[g])
    for g in sorted(missing):
        users = [entries[x].path for x in need if g in entries[x].refs]
        out('  THIẾU     guid %s, trỏ từ %s' % (g, ', '.join(users[:3])))

    rest = [e for e in files if e.guid not in need]
    out('')
    out('không cần cho gốc: %d file, %s' % (len(rest), human(sum(e.size for e in rest))))
    groups = defaultdict(lambda: defaultdict(int))
    for e in rest:
        groups[group_key(e.path)][status[e.guid][0]] += 1
    for key in sorted(groups):
        counts = groups[key]
        out('  %-44s %s' % (key, ' · '.join('%s %d' % (k, counts[k]) for k in sorted(counts))))

    risky = [e for e in rest if status[e.guid][0] in ('khác bản', 'trùng đường dẫn khác guid')]
    if risky:
        out('')
        out('import cả gói sẽ chạm %d file dự án đang có mà gốc không cần:' % len(risky))
        shown = risky if list_all else risky[:15]
        for e in shown:
            st, where = status[e.guid]
            out('  %-26s %s%s' % (st, e.path, (' (dự án: %s)' % where) if where else ''))
        if len(shown) < len(risky):
            out('  … %d file nữa, thêm --list để in hết' % (len(risky) - len(shown)))
    if list_all:
        out('')
        out('mọi file không cần cho gốc:')
        for e in rest:
            out('  %-26s %9s  %s' % (status[e.guid][0], human(e.size), e.path))
    return dict(entries=entries, status=status, need=need, missing=missing, in_project=in_project, builtin=builtin)


# ---------------------------------------------------------------- selftest

def selftest():
    import tempfile
    tmp = tempfile.mkdtemp(prefix='unitypackage_audit_')
    proj = os.path.join(tmp, 'proj')

    def guid(n):
        return ('%032x' % n)

    def write_project(rel, data, g):
        full = os.path.join(proj, rel)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, 'wb') as fh:
            fh.write(data)
        with open(full + '.meta', 'w', encoding='utf-8') as fh:
            fh.write('fileFormatVersion: 2\nguid: %s\n' % g)

    def yaml(*refs):
        body = b'%YAML 1.1\n--- !u!1 &1\n'
        for r in refs:
            body += b'  m_Ref: {fileID: 2100000, guid: ' + r.encode() + b', type: 2}\n'
        return body

    g_prefab, g_mat, g_tex, g_script, g_same, g_diff, g_new, g_dir, g_lib = (guid(i) for i in range(1, 10))
    builtin = '0000000000000000f000000000000000'
    # dự án: script của thư viện có sẵn, một file thư viện giống, một file khác bản, một file trùng đường dẫn khác guid
    write_project('Assets/Lib/Runtime/Comp.cs', b'class Comp {}\r\n', g_script)
    write_project('Assets/Lib/Runtime/Same.cs', b'class Same {}\r\n', g_same)
    write_project('Assets/Lib/Runtime/Diff.cs', b'class Diff { int v = 2; }\n', g_diff)
    write_project('Assets/Lib/Readme.txt', b'cu', guid(99))
    write_project('Assets/MyFX/glow.png', b'\x89PNG....', g_tex)

    pkg = [
        (g_dir, 'Assets/FX', None),
        (g_prefab, 'Assets/FX/Win.prefab', yaml(g_mat, g_script, builtin)),
        (g_mat, 'Assets/FX/Glow.mat', yaml(g_tex)),
        (g_tex, 'Assets/FX/glow.png', b'\x89PNG....'),
        (g_script, 'Assets/Lib/Runtime/Comp.cs', b'class Comp {}\n'),
        (g_same, 'Assets/Lib/Runtime/Same.cs', b'class Same {}\n'),
        (g_diff, 'Assets/Lib/Runtime/Diff.cs', b'class Diff { int v = 1; }\n'),
        (g_new, 'Assets/Lib/Examples/Demo.cs', b'class Demo {}\n'),
        (g_lib, 'Assets/Lib/Readme.txt', b'moi'),
    ]
    package = os.path.join(tmp, 'fx.unitypackage')
    with tarfile.open(package, 'w:gz') as tar:
        def add(name, data):
            info = tarfile.TarInfo(name)
            info.size = len(data)
            tar.addfile(info, io.BytesIO(data))
        for g, path, data in pkg:
            add('%s/pathname' % g, (path + '\n00').encode())
            add('%s/asset.meta' % g, ('fileFormatVersion: 2\nguid: %s\n' % g).encode())
            if data is not None:
                add('%s/asset' % g, data)

    lines = []
    r = audit(package, proj, [], False, out=lines.append)
    st = {r['entries'][g].path: s for g, (s, _) in r['status'].items()}
    need = {r['entries'][g].path for g in r['need']}
    fails = []

    def check(cond, msg):
        print('  %s %s' % ('ok  ' if cond else 'SAI ', msg))
        if not cond:
            fails.append(msg)

    check(need == {'Assets/FX', 'Assets/FX/Win.prefab', 'Assets/FX/Glow.mat', 'Assets/FX/glow.png', 'Assets/Lib/Runtime/Comp.cs'},
          'gốc kéo theo prefab, material, texture, script và thư mục cha (%s)' % ', '.join(sorted(need)))
    check(st['Assets/Lib/Runtime/Comp.cs'] == 'giống', 'script khác mỗi CRLF là giống (%s)' % st['Assets/Lib/Runtime/Comp.cs'])
    check(st['Assets/Lib/Runtime/Diff.cs'] == 'khác bản', 'file thư viện khác nội dung là khác bản (%s)' % st['Assets/Lib/Runtime/Diff.cs'])
    check(st['Assets/Lib/Examples/Demo.cs'] == 'mới', 'file chưa có là mới (%s)' % st['Assets/Lib/Examples/Demo.cs'])
    check(st['Assets/Lib/Readme.txt'] == 'trùng đường dẫn khác guid', 'cùng đường dẫn khác guid (%s)' % st['Assets/Lib/Readme.txt'])
    check(r['builtin'] == {builtin} and not r['missing'], 'guid dựng sẵn không tính là thiếu')
    tex = r['status'][g_tex]
    check(tex == ('giống', 'Assets/MyFX/glow.png'), 'file đã dời chỗ trong dự án vẫn so nội dung, báo chỗ mới (%s)' % (tex,))
    check(any('khác bản' in l and 'Diff.cs' in l for l in lines), 'cảnh báo file khác bản ngoài gốc')
    r2 = audit(package, proj, ['Glow.mat'], False, out=lambda *_: None)
    need2 = {r2['entries'][g].path for g in r2['need']}
    check(need2 == {'Assets/FX', 'Assets/FX/Glow.mat', 'Assets/FX/glow.png'}, '--root chọn gốc theo chuỗi (%s)' % ', '.join(sorted(need2)))
    if fails:
        sys.exit('selftest: %d phép kiểm sai' % len(fails))
    print('selftest: mọi phép kiểm khớp đáp án dựng sẵn')


def main():
    if len(sys.argv) == 2 and sys.argv[1] == 'selftest':
        selftest()
        return
    p = argparse.ArgumentParser(description='Soát .unitypackage trước khi import. Xem docs/Editor/UnityPackageAudit.md.')
    p.add_argument('package')
    p.add_argument('--project', default=None, help='thư mục gốc dự án (chứa Assets/); bỏ trống thì chỉ liệt kê gói')
    p.add_argument('--root', action='append', default=[], help='chuỗi nằm trong đường dẫn của gốc cần dùng; lặp cờ cho nhiều gốc. Mặc định mọi prefab, scene')
    p.add_argument('--list', action='store_true', help='in mọi file không cần cho gốc')
    a = p.parse_args()
    project = os.path.abspath(a.project) if a.project else None
    if project and not os.path.isdir(os.path.join(project, 'Assets')):
        sys.exit('unitypackage_audit: %s không có thư mục Assets' % project)
    audit(a.package, project, a.root, a.list)


if __name__ == '__main__':
    main()
