#!/usr/bin/env python3
"""Fail on missing literal UI keys, incomplete bilingual resources or malformed placeholders."""
import re
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]


def check():
    source=(ROOT/'modern/GARbro.UI/DesktopState.cs').read_text()
    data=re.search(r'const string Data = """\n(.*?)\n""";',source,re.S).group(1)
    resources={}
    for row in data.splitlines():
        fields=row.split('|')
        if len(fields)!=3 or not all(fields):raise ValueError('Incomplete bilingual row: '+row)
        key,en,zh=fields
        if key in resources:raise ValueError('Duplicate UI key: '+key)
        if set(re.findall(r'\{\d+\}',en))!=set(re.findall(r'\{\d+\}',zh)):raise ValueError('Translation placeholders differ: '+key)
        resources[key]=(en,zh)
    missing=[]
    for directory in ('GARbro.UI','GARbro.Android'):
        for file in (ROOT/'modern'/directory).glob('*.cs'):
            text=file.read_text()
            keys=re.findall(r'\b(?:Text|text)\["([^"\n]+)"\]',text)
            keys+=re.findall(r'\b(?:Bind|Label|Button|Item|Group|IconButton|ActionButton)\("([^"\n]+)"',text)
            for key in keys:
                if key not in resources:missing.append(str(file.relative_to(ROOT))+': '+key)
    if missing:raise ValueError('Untranslated UI keys:\n'+'\n'.join(missing))
    diagnostics=(ROOT/'modern/GARbro.UI/DesktopText.Errors.cs').read_text()
    diagnostic_data=re.search(r'const string ErrorMessages = """\n(.*?)\n""";',diagnostics,re.S).group(1)
    translations={}
    for row in diagnostic_data.splitlines():
        pair=row.split('|')
        if len(pair)!=2 or not all(pair) or pair[0] in translations:raise ValueError('Invalid bilingual diagnostic: '+row)
        translations[pair[0]]=pair[1]
    for file in (ROOT/'modern/GARbro.Android').glob('*.cs'):
        if file.name in ('AndroidSelfTest.cs','TestDocumentsProvider.cs'):continue
        for literal in re.findall(r'new (?:IOException|InvalidDataException|NotSupportedException|FileNotFoundException|WorkerLimitException)\("([^"\n]+)"',file.read_text()):
            if not any(literal.startswith(prefix) for prefix in translations):raise ValueError('Untranslated Android diagnostic: '+literal)
    for language in ('en-US','zh-CN'):
        for name in ('manual','troubleshoot'):
            if not (ROOT/f'docs/{name}.{language}.md').read_text().strip():raise ValueError('Offline help is empty')
    return len(resources)


if __name__=='__main__':print('Verified',check(),'bilingual UI keys and offline documents')
