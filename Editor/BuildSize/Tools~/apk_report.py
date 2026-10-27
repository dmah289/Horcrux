"""Mổ build Android: byte nằm ở đâu trong APK, bundle Addressables chứa gì, dex thuộc SDK nào.
Xem Editor/BuildSize/BuildSize.md.

  apk_report.py <apk> [--layout [buildlayout.json]] [--editor-log [Editor.log]]
                      [--sections apk,aa,dex,layout,log] [--top N]

Không có đối số đi kèm: --layout lấy file mới nhất trong Library/com.unity.addressables/BuildReports/,
--editor-log lấy %LOCALAPPDATA%/Unity/Editor/Editor.log.
"""
import argparse
import collections
import glob
import json
import os
import re
import statistics
import struct
import sys
import zipfile

MB = 1048576.0
KB = 1024.0
ALL_SECTIONS = ('apk', 'aa', 'dex', 'layout', 'log')


def mb(n):
    return '%8.2f MB' % (n / MB)


def kb(n):
    return '%9.0f KB' % (n / KB)


# ---------------------------------------------------------------- APK

def apk_top_key(name):
    parts = name.split('/')
    if parts[0] == 'lib' and len(parts) > 2:
        return '/'.join(parts[:3])
    if parts[0] == 'assets' and len(parts) > 2:
        return '/'.join(parts[:2])
    if re.fullmatch(r'classes\d*\.dex', parts[0]):
        return 'classes*.dex'
    return parts[0]


def group_by(infos, key):
    acc = collections.defaultdict(lambda: [0, 0, 0])
    for info in infos:
        k = key(info.filename)
        if k is None:
            continue
        row = acc[k]
        row[0] += info.compress_size
        row[1] += info.file_size
        row[2] += 1
    return sorted(acc.items(), key=lambda kv: -kv[1][0])


def print_rows(rows, limit):
    for key, (c, u, n) in rows[:limit]:
        print('%s %s %5d  %s' % (mb(c), mb(u), n, key))


def section_apk(zf, top):
    infos = zf.infolist()
    total_c = sum(i.compress_size for i in infos)
    total_u = sum(i.file_size for i in infos)
    print('== APK: %d entry, nén %.1f MB, chưa nén %.1f MB (cột: nén · chưa nén · số file)' % (len(infos), total_c / MB, total_u / MB))
    print_rows(group_by(infos, apk_top_key), top)

    def bin_key(name):
        prefix = 'assets/bin/Data/'
        if not name.startswith(prefix):
            return None
        parts = name[len(prefix):].split('/')
        return parts[0] if len(parts) == 1 else parts[0] + '/'

    print('\n== assets/bin/Data (player data: scene, Resources, tham chiếu thẳng)')
    print_rows(group_by(infos, bin_key), top)
    print('\n== Entry lớn nhất')
    for info in sorted(infos, key=lambda i: -i.compress_size)[:top]:
        print('%s %s  %s' % (mb(info.compress_size), mb(info.file_size), info.filename))


# ---------------------------------------------------------------- Addressables trong APK

def bundle_group_from_name(name):
    key = re.sub(r'_[0-9a-f]{32}\.bundle$', '', name.split('/')[-1])
    return key.split('_assets_')[0] if '_assets_' in key else key.split('_')[0]


def section_aa(zf, layout, top):
    bundles = [i for i in zf.infolist() if i.filename.startswith('assets/aa/') and i.filename.endswith('.bundle')]
    if not bundles:
        print('== Addressables: không có assets/aa trong APK')
        return
    by_file = layout.bundle_group_names() if layout else {}
    acc = collections.defaultdict(list)
    for info in bundles:
        name = info.filename.split('/')[-1]
        acc[by_file.get(name) or bundle_group_from_name(name)].append(info.compress_size)
    total = sum(i.compress_size for i in bundles)
    print('== Addressables trong APK: %d bundle, nén %.2f MB (cột: nén · số bundle · nhỏ nhất · trung vị · lớn nhất)' % (len(bundles), total / MB))
    for group, sizes in sorted(acc.items(), key=lambda kv: -sum(kv[1])):
        sizes.sort()
        print('%s %5d %s %s %s  %s' % (mb(sum(sizes)), len(sizes), kb(sizes[0]), kb(statistics.median(sizes)), kb(sizes[-1]), group))
    print('\n== Bundle lớn nhất')
    for info in sorted(bundles, key=lambda i: -i.compress_size)[:top]:
        print('%s  %s' % (mb(info.compress_size), info.filename.split('/')[-1]))


# ---------------------------------------------------------------- Build layout của Addressables

class Layout:
    """buildlayout JSON của Addressables: mọi object lồng nhau ghi dưới dạng {"rid": n}, tra ở references.RefIds."""

    def __init__(self, path):
        data = json.load(open(path, encoding='utf-8'))
        self.refs = {r['rid']: r['data'] for r in data['references']['RefIds']}
        self.groups = [self.resolve(g) for g in data.get('Groups', [])]
        self.bundles = [b for b in self.refs.values() if isinstance(b, dict) and 'FileSize' in b and 'LoadPath' in b]

    def resolve(self, obj):
        if isinstance(obj, dict) and set(obj.keys()) == {'rid'}:
            return self.refs.get(obj['rid'], {})
        return obj

    def items(self, lst):
        return [self.resolve(x) for x in (lst or [])]

    def group_name(self, bundle):
        return self.resolve(bundle.get('Group')).get('Name', '?')

    def bundle_group_names(self):
        return {b['Name']: self.group_name(b) for b in self.bundles}

    def assets(self, bundle):
        """(cỡ, đường dẫn, explicit|implicit); implicit là asset bị kéo theo vì có explicit tham chiếu."""
        out = []
        for f in self.items(bundle['Files']):
            for a in self.items(f.get('Assets')):
                out.append((a.get('SerializedSize', 0) + a.get('StreamedSize', 0), a.get('AssetPath', '?'), 'explicit'))
            for a in self.items(f.get('OtherAssets')):
                out.append((a.get('SerializedSize', 0) + a.get('StreamedSize', 0), a.get('AssetPath', '?'), 'implicit'))
        return sorted(out, reverse=True)


ASSET_FLOOR = 8 * KB  # dưới cỡ này asset không đổi kết luận về bundle; icon lẻ trong atlas chiếm hàng trăm dòng


def section_layout(layout, top):
    print('== Build layout: %d group, %d bundle (cỡ asset là chưa nén, trong bundle; bỏ asset dưới %d KB)' % (len(layout.groups), len(layout.bundles), ASSET_FLOOR / KB))
    by_group = collections.defaultdict(list)
    for b in layout.bundles:
        by_group[layout.group_name(b)].append(b)
    for group, bundles in sorted(by_group.items(), key=lambda kv: -sum(b['FileSize'] for b in kv[1])):
        bundles.sort(key=lambda b: -b['FileSize'])
        print('\n-- %s: %d bundle, %.2f MB' % (group, len(bundles), sum(b['FileSize'] for b in bundles) / MB))
        for b in bundles[:3]:
            print('   %s  %s' % (mb(b['FileSize']), b['Name']))
            for size, path, kind in [a for a in layout.assets(b) if a[0] >= ASSET_FLOOR][:top]:
                print('      %s  %-8s %s' % (kb(size), kind, path))
    waste = collections.Counter()
    sizes = {}
    for b in layout.bundles:
        for size, path, kind in layout.assets(b):
            if kind == 'implicit':
                waste[path] += 1
                sizes[path] = size
    print('\n== Asset implicit nằm trong nhiều bundle (làm addressable để chỉ đóng một lần)')
    rows = sorted(((n - 1) * sizes[p], n, p) for p, n in waste.items() if n > 1)
    for wasted, n, path in reversed(rows[-top:]):
        print('   x%-3d %s mỗi bản, thừa %s  %s' % (n, kb(sizes[path]), kb(wasted), path))


# ---------------------------------------------------------------- dex

def uleb128(buf, offset):
    result = 0
    shift = 0
    while True:
        byte = buf[offset]
        offset += 1
        result |= (byte & 0x7f) << shift
        shift += 7
        if byte < 0x80:
            return result, offset


def dex_package_key(descriptor):
    parts = descriptor[1:].split('/')
    depth = 3 if parts[0] in ('com', 'io', 'org', 'net', 'sg', 'me') else 2
    return '.'.join(parts[:depth])


def dex_code_bytes(buf, acc):
    """Quy mỗi class_def về package: 64 B header + 24 B mỗi method + code_item (insns_size * 2). Xấp xỉ, đủ để so SDK với nhau."""
    string_count, string_off = struct.unpack_from('<II', buf, 0x38)
    type_count, type_off = struct.unpack_from('<II', buf, 0x40)
    class_count, class_off = struct.unpack_from('<II', buf, 0x60)

    def string_at(index):
        start = struct.unpack_from('<I', buf, string_off + index * 4)[0]
        _, begin = uleb128(buf, start)
        end = buf.index(b'\0', begin)
        return buf[begin:end].decode('utf-8', 'replace')

    for i in range(class_count):
        class_idx, _, _, _, _, _, class_data_off, _ = struct.unpack_from('<8I', buf, class_off + i * 32)
        key = dex_package_key(string_at(struct.unpack_from('<I', buf, type_off + class_idx * 4)[0]))
        size = 64
        if class_data_off:
            offset = class_data_off
            static_fields, offset = uleb128(buf, offset)
            instance_fields, offset = uleb128(buf, offset)
            direct_methods, offset = uleb128(buf, offset)
            virtual_methods, offset = uleb128(buf, offset)
            for _ in range(static_fields + instance_fields):
                _, offset = uleb128(buf, offset)
                _, offset = uleb128(buf, offset)
            for _ in range(direct_methods + virtual_methods):
                _, offset = uleb128(buf, offset)
                _, offset = uleb128(buf, offset)
                code_off, offset = uleb128(buf, offset)
                size += 24
                if code_off:
                    size += 16 + struct.unpack_from('<I', buf, code_off + 12)[0] * 2
        acc[key] += size


def section_dex(zf, top):
    acc = collections.Counter()
    dex_files = [i for i in zf.infolist() if re.fullmatch(r'classes\d*\.dex', i.filename)]
    for info in dex_files:
        dex_code_bytes(zf.read(info), acc)
    total = sum(acc.values())
    print('== Java dex: %d file, mã xấp xỉ %.1f MB chưa nén (gom theo package, so SDK với nhau)' % (len(dex_files), total / MB))
    for key, size in acc.most_common(top):
        print('%s %5.1f%%  %s' % (mb(size), 100.0 * size / total, key))


# ---------------------------------------------------------------- Editor.log

def section_log(path, top):
    """Khối 'Build Report' cuối cùng trong Editor.log: thể loại rồi danh sách asset của player data, cỡ chưa nén."""
    text = open(path, encoding='utf-8', errors='replace').read()
    start = text.rfind('\nBuild Report\n')
    if start < 0:
        print('== Editor.log: không có khối Build Report (build player chưa chạy trong phiên Editor này)')
        return
    lines = text[start + 1:].split('\n')
    print('== Editor.log Build Report (player data, chưa nén)')
    assets = []
    in_assets = False
    for line in lines[1:]:
        if line.startswith('Used Assets'):
            in_assets = True
            continue
        if in_assets:
            m = re.match(r'\s*([\d.]+ [kmg]b)\s+[\d.]+%\s+(.*)$', line)
            if not m:
                break
            assets.append((m.group(1), m.group(2)))
            continue
        if not line.strip() or line.startswith('---'):
            break
        print('   ' + line.rstrip())
    print('   -- %d asset, lớn nhất:' % len(assets))
    for size, path in assets[:top]:
        print('   %10s  %s' % (size, path))


# ---------------------------------------------------------------- main

def default_layout():
    files = glob.glob(os.path.join('Library', 'com.unity.addressables', 'BuildReports', 'buildlayout_*.json'))
    return max(files, key=os.path.getmtime) if files else None


def default_editor_log():
    return os.path.join(os.environ.get('LOCALAPPDATA', ''), 'Unity', 'Editor', 'Editor.log')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('apk')
    parser.add_argument('--layout', nargs='?', const='auto', help='buildlayout JSON của Addressables; không kèm đường dẫn thì lấy file mới nhất')
    parser.add_argument('--editor-log', nargs='?', const='auto', help='Editor.log có khối Build Report; không kèm đường dẫn thì lấy bản mặc định')
    parser.add_argument('--sections', default=','.join(ALL_SECTIONS), help='chọn mục in: ' + ','.join(ALL_SECTIONS))
    parser.add_argument('--top', type=int, default=20, help='số dòng mỗi bảng')
    args = parser.parse_args()

    sections = [s.strip() for s in args.sections.split(',')]
    unknown = [s for s in sections if s not in ALL_SECTIONS]
    if unknown:
        parser.error('mục không có: %s' % ', '.join(unknown))

    layout = None
    if args.layout:
        path = default_layout() if args.layout == 'auto' else args.layout
        if not path or not os.path.isfile(path):
            parser.error('không thấy buildlayout JSON (%s)' % (path or 'Library/com.unity.addressables/BuildReports/ trống'))
        layout = Layout(path)
        print('layout: %s\n' % path)
    explicit = args.sections != ','.join(ALL_SECTIONS)
    if layout is None and explicit and 'layout' in sections:
        parser.error('mục layout cần --layout')
    if not args.editor_log and explicit and 'log' in sections:
        parser.error('mục log cần --editor-log')

    zf = zipfile.ZipFile(args.apk)
    print('apk: %s (%.1f MB trên đĩa)\n' % (args.apk, os.path.getsize(args.apk) / MB))
    printed = False
    for section in sections:
        if section == 'layout' and layout is None:
            continue
        if section == 'log' and not args.editor_log:
            continue
        if printed:
            print()
        printed = True
        if section == 'apk':
            section_apk(zf, args.top)
        elif section == 'aa':
            section_aa(zf, layout, args.top)
        elif section == 'dex':
            section_dex(zf, args.top)
        elif section == 'layout':
            section_layout(layout, args.top)
        elif section == 'log':
            path = default_editor_log() if args.editor_log == 'auto' else args.editor_log
            section_log(path, args.top)


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    main()
