import argparse
import xml.etree.ElementTree as ET
import json
import html
import re
from pathlib import Path
from collections import OrderedDict

# 配置：根据你的 Story.xml 可进一步调整
ID_ATTRS = ["id", "Id", "ID", "name", "Name"]
LABEL_ATTRS = ["label", "text", "title", "name"]
TARGET_ATTRS = ["target", "to", "goto", "next", "ref", "idref", "targetId"]
CHILD_TAG_TARGETS = ["choice", "option", "link", "goto", "jump", "action"]
INLINE_GOTO_RE = re.compile(r"(?:goto|jump|->)\s*#?([A-Za-z0-9_\-:]+)", re.IGNORECASE)


def find_id(elem):
    for a in ID_ATTRS:
        v = elem.get(a)
        if v:
            return v.strip()
    return None


def label_for(elem, fallback=None, maxlen=60):
    for a in LABEL_ATTRS:
        v = elem.get(a)
        if v:
            s = v.strip()
            return (s[:maxlen] + "...") if len(s) > maxlen else s
    text = (elem.text or "").strip()
    if text:
        s = text.replace("\n", " ").strip()
        return (s[:maxlen] + "...") if len(s) > maxlen else s
    return fallback or elem.tag


def collect_nodes(root):
    """
    按文档顺序收集带 id 的元素。返回 OrderedDict: id -> node dict (包含 order index)。
    """
    nodes = OrderedDict()
    idx = 0
    for elem in root.iter():
        node_id = find_id(elem)
        if node_id:
            # 防止同一 id 重复出现，保留第一个出现的
            if node_id not in nodes:
                nodes[node_id] = {
                    "id": node_id,
                    "label": label_for(elem, fallback=elem.tag),
                    "tag": elem.tag,
                    "elem": elem,
                    "order": idx
                }
                idx += 1
    return nodes


def next_node_id(nodes_ordered, src_id):
    """
    找到按顺序的下一个节点 id；若没有下一个返回 None。
    nodes_ordered: OrderedDict of nodes
    """
    keys = list(nodes_ordered.keys())
    try:
        i = keys.index(src_id)
    except ValueError:
        return None
    if i + 1 < len(keys):
        return keys[i + 1]
    return None


def collect_edges(nodes):
    """
    根据约定规则收集边关系（有向）。
    规则详见文件顶部说明。
    """
    edges = []
    seen = set()  # 去重 (from,to,rel)
    for node_id, node in nodes.items():
        src_elem = node["elem"]
        src_id = node_id
        outgoing_count = 0

        # 1) 节点自身的显式目标属性
        for a in TARGET_ATTRS:
            val = src_elem.get(a)
            if val:
                for part in re.split(r"[,;/\s]+", val.strip()):
                    if not part:
                        continue
                    tgt = part.lstrip("#")
                    key = (src_id, tgt, a)
                    if key not in seen:
                        edges.append((src_id, tgt, a))
                        seen.add(key)
                        outgoing_count += 1

        # 2) inline goto（文本中）
        txt = (src_elem.text or "")
        for m in INLINE_GOTO_RE.finditer(txt):
            tgt = m.group(1)
            key = (src_id, tgt, "inline")
            if key not in seen:
                edges.append((src_id, tgt, "inline"))
                seen.add(key)
                outgoing_count += 1

        # 3) 处理选项子元素（按约定选择标签）
        option_children = [c for c in list(src_elem) if c.tag.lower() in CHILD_TAG_TARGETS or any(k in c.attrib for k in TARGET_ATTRS)]
        if option_children:
            if len(option_children) == 1:
                child = option_children[0]
                child_found = False
                for a in TARGET_ATTRS:
                    v = child.get(a)
                    if v:
                        for part in re.split(r"[,;/\s]+", v.strip()):
                            if not part:
                                continue
                            tgt = part.lstrip("#")
                            key = (src_id, tgt, f"child::{child.tag}@{a}")
                            if key not in seen:
                                edges.append((src_id, tgt, f"child::{child.tag}@{a}"))
                                seen.add(key)
                                outgoing_count += 1
                        child_found = True
                # 选项没有指定目标，按约定连向下一个节点
                if not child_found:
                    nxt = next_node_id(nodes, src_id)
                    if nxt:
                        key = (src_id, nxt, "child::default::next")
                        if key not in seen:
                            edges.append((src_id, nxt, "child::default::next"))
                            seen.add(key)
                            outgoing_count += 1
            else:
                # 多选项：每个选项独立判断，指定 goto 的连向目标；未指定的连向下一个节点
                for child in option_children:
                    child_found = False
                    for a in TARGET_ATTRS:
                        v = child.get(a)
                        if v:
                            for part in re.split(r"[,;/\s]+", v.strip()):
                                if not part:
                                    continue
                                tgt = part.lstrip("#")
                                key = (src_id, tgt, f"child::{child.tag}@{a}")
                                if key not in seen:
                                    edges.append((src_id, tgt, f"child::{child.tag}@{a}"))
                                    seen.add(key)
                                    outgoing_count += 1
                            child_found = True
                    if not child_found:
                        # 未明确目标的选项 —— 按约定连向下一个节点
                        nxt = next_node_id(nodes, src_id)
                        if nxt:
                            key = (src_id, nxt, f"child::{child.tag}::default::next")
                            if key not in seen:
                                edges.append((src_id, nxt, f"child::{child.tag}::default::next"))
                                seen.add(key)
                                outgoing_count += 1

        # 4) 如果没有任何出边，根据约定默认连向下一个节点
        if outgoing_count == 0:
            nxt = next_node_id(nodes, src_id)
            if nxt:
                key = (src_id, nxt, "default::next")
                if key not in seen:
                    edges.append((src_id, nxt, "default::next"))
                    seen.add(key)
                    outgoing_count += 1
            # 如果没有下一个节点（最后一个节点），则不生成默认边

    return edges


def build_interactive_html(nodes, edges, out_path, use_label=False):
    # 默认显示节点 id；若 use_label=True 则显示原始 label（旧行为）
    if use_label:
        nodes_arr = [{"id": n["id"], "label": n["label"], "title": html.escape(f"{n['tag']} / {n['id']}")} for n in nodes.values()]
    else:
        nodes_arr = [{"id": n["id"], "label": n["id"], "title": html.escape(f"{n['tag']} / {n['id']}\n{n['label']}")} for n in nodes.values()]
    edges_arr = [{"from": a, "to": b, "label": rel} for (a, b, rel) in edges]
    template = f"""<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  <title>Story Graph</title>
  <script type="text/javascript" src="https://unpkg.com/vis-network@9.1.2/standalone/umd/vis-network.min.js"></script>
  <style>
    body {{ font-family: Arial, sans-serif; margin: 0; }}
    #mynetwork {{ width: 100%; height: 100vh; border: 1px solid #ddd; }}
  </style>
</head>
<body>
<div id="mynetwork"></div>
<script>
const nodes = {json.dumps(nodes_arr, ensure_ascii=False)};
const edges = {json.dumps(edges_arr, ensure_ascii=False)};
const container = document.getElementById('mynetwork');
const data = {{
  nodes: new vis.DataSet(nodes),
  edges: new vis.DataSet(edges)
}};
const options = {{
  nodes: {{
    shape: 'box',
    margin: 10,
    widthConstraint: {{ maximum: 300 }}
  }},
  edges: {{
    arrows: 'to',
    smooth: {{ type: 'cubicBezier' }}
  }},
  physics: {{
    stabilization: false,
    barnesHut: {{
      gravitationalConstant: -20000,
      springConstant: 0.001,
      springLength: 200
    }}
  }},
  interaction: {{ hover: true }}
}};
const network = new vis.Network(container, data, options);

network.on('click', function(params) {{
    if (params.nodes.length > 0) {{
        const nid = params.nodes[0];
        const n = nodes.find(x => x.id === nid);
        alert('节点: ' + nid + '\\n' + (n.title || ''));
    }}
}});
</script>
</body>
</html>"""
    out_path.write_text(template, encoding="utf-8")
    print(f"Wrote interactive HTML to {out_path}")


def build_dot(nodes, edges, out_path, use_label=False):
    lines = ["digraph story {", "  rankdir=LR;", "  node [shape=box];"]
    for n in nodes.values():
        lname = (n['label'] if use_label else n['id']).replace('"', '\\"')
        lines.append(f'  "{n["id"]}" [label="{lname}"];')
    for a, b, rel in edges:
        lines.append(f'  "{a}" -> "{b}" [label="{str(rel).replace("\"", "\\\"")}"];')
    lines.append("}")
    out_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote DOT to {out_path}")


def main():
    ap = argparse.ArgumentParser(description="Parse Story.xml and output graph (HTML or DOT).")
    ap.add_argument("--xml", "-x", required=True, help="Path to Story.xml")
    ap.add_argument("--out", "-o", help="Output HTML path (interactive vis.js).")
    ap.add_argument("--out-dot", help="Output Graphviz DOT path.")
    ap.add_argument("--use-label", action="store_true", help="Display original node label text instead of node id in the graph.")
    args = ap.parse_args()

    p = Path(args.xml)
    if not p.exists():
        print("指定的 XML 文件不存在:", p)
        return

    tree = ET.parse(str(p))
    root = tree.getroot()
    nodes = collect_nodes(root)
    if not nodes:
        # fallback: use immediate children as nodes (生成顺序 id)
        print("未找到带 id 的节点，尝试把第一层子元素当作节点。")
        nodes = OrderedDict()
        for idx, elem in enumerate(list(root)):
            nid = elem.tag + "_" + str(idx)
            nodes[nid] = {"id": nid, "label": label_for(elem), "tag": elem.tag, "elem": elem, "order": idx}

    edges = collect_edges(nodes)

    # 输出
    if args.out:
        outp = Path(args.out)
        build_interactive_html(nodes, edges, outp, use_label=args.use_label)
    if args.out_dot:
        outp2 = Path(args.out_dot)
        build_dot(nodes, edges, outp2, use_label=args.use_label)


if __name__ == "__main__":
    main()