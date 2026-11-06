"""Ghép và kiểm prefab Unity ở mức YAML, không cần Unity: chép cây object giữa hai prefab, xoá cây, đổi field, kiểm cây.

CLI:
  prefab_yaml.py check  <prefab>...
  prefab_yaml.py graft  <dst> <src> <src_object> <dst_parent> [--index N]   ghi đè dst tại chỗ rồi check
  prefab_yaml.py remove <prefab> <object>                                   ghi đè tại chỗ rồi check

<object> là tên GameObject (duy nhất trong prefab) hoặc tên PrefabInstance lồng (m_Name override).
Ghép kèm sửa field thì import module này trong một script ngắn: Prefab, check (chi tiết ở docs/Editor/PrefabYaml.md).
Mã thoát: 0 sạch · 1 check báo lỗi · 2 dùng sai hoặc tên không tìm thấy.
"""
import argparse
import re
import sys

HEADER = re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?$', re.M)
TRANSFORM_CLASSES = ('4', '224')  # Transform, RectTransform


class Doc:
    def __init__(self, cls, fid, stripped, body):
        self.cls, self.fid, self.stripped, self.body = cls, fid, stripped, body

    def text(self):
        return f"--- !u!{self.cls} &{self.fid}{' stripped' if self.stripped else ''}\n{self.body}"


class Prefab:
    def __init__(self, text):
        heads = list(HEADER.finditer(text))
        self.preamble = text[:heads[0].start()]
        self.docs = []
        for i, h in enumerate(heads):
            end = heads[i + 1].start() if i + 1 < len(heads) else len(text)
            body = text[h.end() + 1:end]
            self.docs.append(Doc(h.group(1), h.group(2), bool(h.group(3)), body))
        self.by = {d.fid: d for d in self.docs}

    @classmethod
    def load(cls, path):
        with open(path, encoding='utf-8') as f:
            return cls(f.read())

    def save(self, path):
        # Unity ghi YAML bằng LF trên mọi nền tảng.
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(self.text())

    def text(self):
        return self.preamble + ''.join(d.text() for d in self.docs)

    # ---- tra cứu
    def go_named(self, name):
        hits = [d for d in self.docs if d.cls == '1' and re.search(r'^  m_Name: ' + re.escape(name) + r'$', d.body, re.M)]
        assert len(hits) == 1, f"'{name}': {len(hits)} GameObject"
        return hits[0]

    def instance_named(self, name):
        hits = [d for d in self.docs if d.cls == '1001'
                and re.search(r'propertyPath: m_Name\n\s+value: ' + re.escape(name) + r'\n', d.body)]
        assert len(hits) == 1, f"instance '{name}': {len(hits)}"
        return hits[0]

    def stripped_of(self, instance, cls=TRANSFORM_CLASSES):
        """Doc stripped (mặc định là transform gốc) mà PrefabInstance `instance` sinh ra trong prefab này."""
        marker = f'm_PrefabInstance: {{fileID: {instance.fid}}}'
        hits = [d for d in self.docs if d.stripped and d.cls in cls and marker in d.body]
        assert hits, f'instance {instance.fid}: không có doc stripped lớp {cls}'
        return hits[0]

    def transform_named(self, name):
        """Transform của GameObject tên `name`, hoặc transform gốc của PrefabInstance lồng tên `name`."""
        gos = [d for d in self.docs if d.cls == '1' and re.search(r'^  m_Name: ' + re.escape(name) + r'$', d.body, re.M)]
        if len(gos) == 1:
            return self.transform_of(gos[0])
        assert not gos, f"'{name}': {len(gos)} GameObject"
        instances = [d for d in self.docs if d.cls == '1001'
                     and re.search(r'propertyPath: m_Name\n\s+value: ' + re.escape(name) + r'\n', d.body)]
        assert len(instances) == 1, f"'{name}': {len(instances)} PrefabInstance, 0 GameObject"
        return self.stripped_of(instances[0])

    def transform_of(self, go):
        for c in re.findall(r'component: \{fileID: (-?\d+)\}', go.body):
            if self.by[c].cls in TRANSFORM_CLASSES:
                return self.by[c]
        raise AssertionError(f'no transform for {go.fid}')

    def component_of(self, go, cls):
        """Component đầu tiên lớp `cls` (số !u!, vd '212' SpriteRenderer, '114' MonoBehaviour) của GameObject."""
        return next(self.by[c] for c in re.findall(r'component: \{fileID: (-?\d+)\}', go.body) if self.by[c].cls == cls)

    def children(self, tr):
        m = re.search(r'  m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)', tr.body)
        return re.findall(r'fileID: (-?\d+)', m.group(1)) if m else []

    def father(self, tr):
        return re.search(r'  m_Father: \{fileID: (-?\d+)\}', tr.body).group(1)

    def subtree(self, tr):
        """fileID của transform tr, GameObject, component, và cả cây con (kể cả nested PrefabInstance)."""
        ids = []
        if tr.stripped:
            pi = re.search(r'm_PrefabInstance: \{fileID: (-?\d+)\}', tr.body).group(1)
            ids.append(pi)
            ids += [d.fid for d in self.docs if d.stripped and f'm_PrefabInstance: {{fileID: {pi}}}' in d.body]
            for d in self.docs:
                if d.cls == '1001' and d.fid != pi:
                    p = re.search(r'm_TransformParent: \{fileID: (-?\d+)\}', d.body).group(1)
                    if p in ids:
                        raise AssertionError('nested instance trong instance: chưa hỗ trợ')
            return ids
        go = self.by[re.search(r'm_GameObject: \{fileID: (-?\d+)\}', tr.body).group(1)]
        ids.append(go.fid)
        ids += re.findall(r'component: \{fileID: (-?\d+)\}', go.body)
        for c in self.children(tr):
            ids += self.subtree(self.by[c])
        return ids

    # ---- sửa
    def set_children(self, tr, kids):
        block = '  m_Children:' + ('\n' + '\n'.join(f'  - {{fileID: {k}}}' for k in kids) if kids else ' []')
        tr.body, n = re.subn(r'  m_Children:(?: \[\])?\n(?:  - \{fileID: -?\d+\}\n)*', block + '\n', tr.body, count=1)
        assert n == 1

    def set_father(self, tr, father):
        if tr.stripped:
            pi = self.by[re.search(r'm_PrefabInstance: \{fileID: (-?\d+)\}', tr.body).group(1)]
            pi.body, n = re.subn(r'm_TransformParent: \{fileID: -?\d+\}', f'm_TransformParent: {{fileID: {father}}}', pi.body)
        else:
            tr.body, n = re.subn(r'  m_Father: \{fileID: -?\d+\}', f'  m_Father: {{fileID: {father}}}', tr.body)
        assert n == 1

    def parent_of(self, tr):
        if tr.stripped:
            pi = self.by[re.search(r'm_PrefabInstance: \{fileID: (-?\d+)\}', tr.body).group(1)]
            return self.by.get(re.search(r'm_TransformParent: \{fileID: (-?\d+)\}', pi.body).group(1))
        return self.by.get(self.father(tr))

    def remove_subtree(self, tr):
        ids = set(self.subtree(tr))
        parent = self.parent_of(tr)
        if parent is not None:
            self.set_children(parent, [k for k in self.children(parent) if k != tr.fid])
        self.docs = [d for d in self.docs if d.fid not in ids]
        self.by = {d.fid: d for d in self.docs}
        return ids

    def graft(self, src, src_tr, dst_parent_tr, index=None):
        """Chép cây src_tr của prefab src vào dưới dst_parent_tr, giữ fileID."""
        ids = src.subtree(src_tr)
        clash = [i for i in ids if i in self.by]
        assert not clash, f'fileID trùng: {clash}'
        anchor = self.docs.index(dst_parent_tr) + 1
        copies = [Doc(src.by[i].cls, i, src.by[i].stripped, src.by[i].body) for i in ids]
        self.docs[anchor:anchor] = copies
        self.by = {d.fid: d for d in self.docs}
        root = self.by[src_tr.fid]
        self.set_father(root, dst_parent_tr.fid)
        kids = self.children(dst_parent_tr)
        kids.insert(len(kids) if index is None else index, root.fid)
        self.set_children(dst_parent_tr, kids)
        return ids

    def replace_doc(self, src, fid):
        assert fid in self.by and fid in src.by
        self.by[fid].body = src.by[fid].body

    def set_line(self, doc, key, value):
        """Đổi dòng 'key: ...' đầu tiên (key có thể có thụt lề) trong doc."""
        doc.body, n = re.subn(r'^(\s*)' + re.escape(key) + r': .*$', lambda m: f'{m.group(1)}{key}: {value}', doc.body,
                              count=1, flags=re.M)
        assert n == 1, f'{key}: {n}'

    def delete_line(self, doc, key):
        doc.body, n = re.subn(r'^\s*' + re.escape(key) + r': .*\n', '', doc.body, count=1, flags=re.M)
        assert n == 1, f'{key}: {n}'

    def insert_after(self, doc, key, lines):
        m = re.search(r'^\s*' + re.escape(key) + r': .*\n', doc.body, re.M)
        assert m, key
        doc.body = doc.body[:m.end()] + ''.join(line + '\n' for line in lines) + doc.body[m.end():]


def check(p, label):
    """fileID không trùng · m_Children ↔ m_Father khớp · mọi {fileID: X} không kèm guid trỏ vào doc có thật."""
    errors = []
    seen = set()
    for d in p.docs:
        if d.fid in seen:
            errors.append(f'trùng fileID {d.fid}')
        seen.add(d.fid)
    for d in p.docs:
        if d.cls in TRANSFORM_CLASSES and not d.stripped:
            for c in p.children(d):
                if c not in p.by:
                    errors.append(f'{d.fid} có con {c} không tồn tại')
                    continue
                cd = p.by[c]
                if cd.stripped:
                    pi = p.by[re.search(r'm_PrefabInstance: \{fileID: (-?\d+)\}', cd.body).group(1)]
                    par = re.search(r'm_TransformParent: \{fileID: (-?\d+)\}', pi.body).group(1)
                else:
                    par = p.father(cd)
                if par != d.fid:
                    errors.append(f'con {c} của {d.fid} lại khai cha {par}')
            f = p.father(d)
            if f != '0' and (f not in p.by or d.fid not in p.children(p.by[f])):
                errors.append(f'{d.fid} khai cha {f} nhưng cha không liệt kê nó')
    for d in p.docs:
        for ref in re.findall(r'\{fileID: (-?\d+)\}', d.body):
            if ref != '0' and ref not in p.by:
                errors.append(f'{d.fid} trỏ fileID {ref} không có')
    for d in p.docs:
        if d.cls == '1001':
            par = re.search(r'm_TransformParent: \{fileID: (-?\d+)\}', d.body).group(1)
            if par != '0' and par not in p.by:
                errors.append(f'instance {d.fid} gắn vào {par} không có')
    print(f'[{label}] {len(p.docs)} doc, {len(errors)} lỗi')
    for e in errors[:20]:
        print('  ', e)
    return not errors


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    sub = ap.add_subparsers(dest='cmd', required=True)
    c = sub.add_parser('check')
    c.add_argument('prefabs', nargs='+')
    g = sub.add_parser('graft')
    g.add_argument('dst')
    g.add_argument('src')
    g.add_argument('src_object')
    g.add_argument('dst_parent')
    g.add_argument('--index', type=int, help='vị trí trong m_Children của dst_parent; mặc định cuối')
    r = sub.add_parser('remove')
    r.add_argument('prefab')
    r.add_argument('object')
    a = ap.parse_args(argv)

    try:
        if a.cmd == 'check':
            return 0 if all([check(Prefab.load(f), f) for f in a.prefabs]) else 1
        if a.cmd == 'graft':
            dst, src = Prefab.load(a.dst), Prefab.load(a.src)
            ids = dst.graft(src, src.transform_named(a.src_object), dst.transform_named(a.dst_parent), a.index)
            dst.save(a.dst)
            print(f'graft {a.src_object} → {a.dst_parent}: {len(ids)} doc')
            return 0 if check(Prefab.load(a.dst), a.dst) else 1
        p = Prefab.load(a.prefab)
        ids = p.remove_subtree(p.transform_named(a.object))
        p.save(a.prefab)
        print(f'remove {a.object}: {len(ids)} doc')
        return 0 if check(Prefab.load(a.prefab), a.prefab) else 1
    except AssertionError as e:
        print(f'[prefab_yaml]: {e}', file=sys.stderr)
        return 2


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
