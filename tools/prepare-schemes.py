#!/usr/bin/env python3
"""Prepare deterministic portable data from this repository's legacy Windows export."""
import argparse
import gzip
import hashlib
import io
import json
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('export',type=Path)
args=parser.parse_args()
source=json.loads(args.export.read_text(encoding='utf-8-sig'))
tags=['PACK/QLIE','NPK','NPA','YPF','NS2','NSA','XP3']
data={'Version':source['Version'],'SchemeMap':{tag:source['SchemeMap'][tag]for tag in tags},'GameMap':source['GameMap']}
raw=json.dumps(data,ensure_ascii=False,separators=(',',':')).encode()
out=Path(__file__).resolve().parents[1]/'modern/GameData/Formats.json.gz'
with out.open('wb')as stream:
 with gzip.GzipFile(filename='',mode='wb',fileobj=stream,mtime=0,compresslevel=9)as encoded:encoded.write(raw)
print('Prepared',len(tags),'scheme groups;',out.stat().st_size,'compressed bytes; source version',source['Version'])
