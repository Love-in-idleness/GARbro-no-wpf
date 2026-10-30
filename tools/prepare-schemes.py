#!/usr/bin/env python3
"""Prepare deterministic portable data from this repository's legacy Windows export."""
import argparse
import gzip
import json
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('export',type=Path)
parser.add_argument('--report',type=Path,help='Successful import report from the target portable catalog')
args=parser.parse_args()
source=json.loads(args.export.read_text(encoding='utf-8-sig'))
tags=['PACK/QLIE','NPK','NPA','YPF','NS2','NSA','XP3']
if args.report:
 report=json.loads(args.report.read_text(encoding='utf-8-sig'))
 if report['failed']or report['version']!=source['Version']:
  raise ValueError('Resolve all import failures and match source versions before bundling data')
 tags=sorted(report['applied'])
data={'Version':source['Version'],'SchemeMap':{tag:source['SchemeMap'][tag]for tag in tags},'GameMap':source['GameMap']}
if 'PortableSchemaVersion'in source:data['PortableSchemaVersion']=source['PortableSchemaVersion']
raw=json.dumps(data,ensure_ascii=False,separators=(',',':')).encode()
out=Path(__file__).resolve().parents[1]/'modern/GameData/Formats.json.gz'
with out.open('wb')as stream:
 with gzip.GzipFile(filename='',mode='wb',fileobj=stream,mtime=0,compresslevel=9)as encoded:encoded.write(raw)
print('Prepared',len(tags),'scheme groups;',out.stat().st_size,'compressed bytes; source version',source['Version'])
