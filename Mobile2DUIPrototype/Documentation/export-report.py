"""Offline structural validation and Markdown export; does not execute HTML."""
from html.parser import HTMLParser
from pathlib import Path
import re
import json

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent

class Element:
    def __init__(self, tag, attrs=()):
        self.tag, self.attrs, self.children = tag, dict(attrs), []

class Parser(HTMLParser):
    VOID = {'meta', 'img', 'br', 'hr', 'input', 'link', 'wbr'}
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.root = Element('document')
        self.stack = [self.root]
        self.mismatches = []
    def handle_starttag(self, tag, attrs):
        node = Element(tag, attrs)
        self.stack[-1].children.append(node)
        if tag not in self.VOID:
            self.stack.append(node)
    def handle_endtag(self, tag):
        if tag in self.VOID: return
        if self.stack[-1].tag != tag:
            self.mismatches.append((self.stack[-1].tag, tag))
        for i in range(len(self.stack)-1, 0, -1):
            if self.stack[i].tag == tag:
                self.stack = self.stack[:i]
                return
    def handle_data(self, data):
        self.stack[-1].children.append(data)

def walk(node):
    if isinstance(node, str): return
    yield node
    for child in node.children: yield from walk(child)

def plain(node):
    if isinstance(node, str): return node
    if node.tag == 'br': return '\n'
    return ''.join(plain(x) for x in node.children)

DIAGRAM = '''
```mermaid
flowchart TD
    Config["MainTabs.asset: 탭 설정"] --> Host["TabHost: 로드·선택·정책"]
    Host --> H[Home]
    Host --> W[Workshop]
    Host --> C[Collection]
    Host --> F[Friends]
    H --> Cam["선택 Scene의 전용 카메라"]
    W --> Cam
    C --> Cam
    F --> Cam
    Cam --> RT[RenderTexture]
    RT --> Raw["공통 UI의 중앙 RawImage"]
    Raw --> Input["MirrorInput: 좌표 변환"]
    Input --> Content["선택 Scene의 버튼 / 2D 도형"]
```
'''

def render(node):
    if isinstance(node, str): return re.sub(r'\s+', ' ', node)
    tag = node.tag
    if tag in {'head', 'style', 'button', 'nav', 'defs'}: return ''
    if tag == 'svg': return DIAGRAM
    if tag == 'pre':
        return '\n\n```\n' + plain(node).strip() + '\n```\n\n'
    if tag == 'img':
        match = re.fullmatch(r'\{\{IMG:([^}]+)\}\}', node.attrs.get('src',''))
        if not match: raise ValueError('Unknown image source')
        path = (ROOT / 'Screenshots' / match[1]).as_posix()
        return f'\n\n![{node.attrs.get("alt", "화면")}]({path})\n\n'
    if tag in {'ol','ul'}:
        items = [x for x in node.children if isinstance(x,Element) and x.tag=='li']
        rows=[]
        for i, item in enumerate(items):
            prefix=f'{i+1}. ' if tag=='ol' else '- '
            rows.append(prefix+''.join(render(x) for x in item.children).strip())
        return '\n\n'+'\n'.join(rows)+'\n\n'
    if tag == 'table':
        rows=[]
        for tr in walk(node):
            if tr.tag != 'tr': continue
            cells = [x for x in tr.children if isinstance(x,Element) and x.tag in {'td','th'}]
            values = [re.sub(r'\s+',' ',plain(x)).strip().replace('|','\\|') for x in cells]
            rows.append('| '+' | '.join(values)+' |')
        count = len([x for x in next(x for x in walk(node) if x.tag=='tr').children if isinstance(x,Element) and x.tag in {'td','th'}])
        rows.insert(1,'| '+' | '.join(['---']*count)+' |')
        return '\n\n'+'\n'.join(rows)+'\n\n'
    content = ''.join(render(x) for x in node.children)
    if tag in {'h1','h2','h3'}: return '\n\n'+'#'*int(tag[1])+' '+re.sub(r'\s+',' ',content).strip()+'\n\n'
    if tag == 'code': return '`'+plain(node).strip()+'`'
    if tag == 'b': return '**'+content.strip()+'**  \n'
    if tag == 'strong': return '**'+content.strip()+'** '
    if tag == 'br': return '  \n'
    if tag in {'p','div','figure','figcaption','header','footer','section'}: return '\n\n'+content.strip()+'\n\n'
    if tag == 'span' and 'arrow' in node.attrs.get('class',''): return '\n\n→\n\n'
    return content

parser=Parser()
parser.feed((HERE/'implementation-report.template.html').read_text(encoding='utf8'))
nodes=list(walk(parser.root))
ids=[x.attrs['id'] for x in nodes if 'id' in x.attrs]
anchors=[x.attrs['href'][1:] for x in nodes if x.tag=='a' and x.attrs.get('href','').startswith('#')]
assert not parser.mismatches, parser.mismatches
assert len(ids)==len(set(ids)), 'duplicate ids'
assert set(anchors)<=set(ids), 'broken contents links'
assert len([x for x in nodes if x.tag=='section'])==16
images=[x for x in nodes if x.tag=='img']
assert len(images)==6
for img in images:
    assert img.attrs.get('alt')
    name=re.fullmatch(r'\{\{IMG:([^}]+)\}\}',img.attrs['src'])[1]
    assert (ROOT/'Screenshots'/name).read_bytes().startswith(b'\x89PNG\r\n\x1a\n')
report=(HERE/'모바일_UI_구현_보고서.html').read_text(encoding='utf8')
assert '{{IMG:' not in report
assert report.count('data:image/png;base64,')==6
assert '<script' not in report
assert 'http://' not in report and 'https://' not in report
main=next(x for x in nodes if x.tag=='main')
markdown=render(main)
markdown=re.sub(r'\n[ \t]+\n','\n\n',markdown)
markdown=re.sub(r'\n{3,}','\n\n',markdown).strip()+'\n'
markdown=markdown.replace('인쇄 / PDF 저장','')
(HERE/'모바일_UI_구현_보고서.md').write_text(markdown,encoding='utf8')
result={'sections':16,'embedded_images':6,'contents_links':len(anchors),'broken_links':0,'tag_mismatches':0,'remote_dependencies':0,'markdown_characters':len(markdown),'visual_browser_check':'Blocked by local-file browser URL policy; not performed.'}
(HERE/'report-validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(result,ensure_ascii=False,indent=2))
