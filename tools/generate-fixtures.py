#!/usr/bin/env python3
"""Deterministic synthetic format fixtures; no commercial-game data or keys."""
from pathlib import Path
import struct as s
import hashlib
import json
import zlib

ROOT=Path(__file__).resolve().parents[1]/'tests/fixtures/generated'
ROOT.mkdir(parents=True,exist_ok=True)
FILES=[('hello.txt',b'Hello, GARbro!\n'),('sub/日本語.bin',bytes(range(32))),('empty.txt',b'')]
manifest=[]
def put(name,tag,data,files,variant):
 (ROOT/name).write_bytes(data)
 manifest.append({'file':name,'format':tag,'variant':variant,'archive_sha256':hashlib.sha256(data).hexdigest(),
  'entries':[{'path':n,'size':len(d),'sha256':hashlib.sha256(d).hexdigest()}for n,d in files]})
def chunk(tag,data):return tag+s.pack('<Q',len(data))+data

def xfl(files):
 index=b'';body=b''
 for name,data in files:
  index+=name.encode('cp932').ljust(32,b'\0')+s.pack('<II',len(body),len(data));body+=data
 return s.pack('<III',0x1424c,len(index),len(files))+index+body
put('standard.xfl','XFL',xfl(FILES),FILES,'raw, nested paths, CP932, empty entry')
put('unsafe.xfl','XFL',xfl([('../escape.txt',b'no')]),[('../escape.txt',b'no')],'path traversal; application must reject')
put('collision.xfl','XFL',xfl([('A.txt',b'A'),('a.txt',b'a')]),[('A.txt',b'A'),('a.txt',b'a')],'case collision; application must reject')
for nsa in [False,True]:
 index=b'';body=b''
 for name,data in FILES:
  index+=name.encode('cp932')+b'\0'+(b'\0'if nsa else b'')+s.pack('>II',len(body),len(data))+(s.pack('>I',len(data))if nsa else b'');body+=data
 put('standard.nsa'if nsa else 'standard.sar','NSA'if nsa else 'SAR',s.pack('>HI',len(FILES),6+len(index))+index+body,FILES,'uncompressed entries')
for version in [6,8]:
 size=4+sum(4+len(n.encode('utf-8'))+12 for n,d in FILES);index=s.pack('<I',len(FILES));body=b''
 for name,data in FILES:
  raw=name.encode('utf-8');index+=s.pack('<I',len(raw))+raw+s.pack('<III',0,7+size+len(body),len(data));body+=data
 if version==8:
  key=hashlib.sha1(index).digest();body=b''
  for name,data in FILES:body+=bytes(v^key[i%len(key)]for i,v in enumerate(data))
 put(f'pf{version}.pfs','PFS',f'pf{version}'.encode()+s.pack('<I',len(index))+index+body,FILES,'pf8 SHA-1 XOR'if version==8 else 'pf6 raw')
for compressed in [False,True]:
 header=b'XP3\r\n \n\x1a\x8b\x67\x01';body=b'';index=b''
 for name,data in FILES:
  packed=zlib.compress(data)if compressed and data else data
  info=s.pack('<IQQH',0,len(data),len(packed),len(name))+name.encode('utf-16-le')
  segment=s.pack('<IQQQ',int(compressed and bool(data)),19+len(body),len(data),len(packed))
  index+=chunk(b'File',chunk(b'info',info)+chunk(b'segm',segment)+chunk(b'adlr',s.pack('<I',zlib.adler32(data))))
  body+=packed
 index_offset=19+len(body)
 if compressed:
  packed=zlib.compress(index);index=b'\1'+s.pack('<QQ',len(packed),len(index))+packed
 else:index=b'\0'+s.pack('<Q',len(index))+index
 put('compressed.xp3'if compressed else 'standard.xp3','XP3',header+s.pack('<Q',index_offset)+body+index,FILES,'zlib entries and index'if compressed else 'uncompressed, segmented')
# RPA-3.0's protocol-2 pickle uses BINSTRING, not executable GLOBAL opcodes.
key=0x12345678;body=b'';index=b'\x80\x02}('
for name,data in FILES:
 raw=name.encode();index+=b'T'+s.pack('<I',len(raw))+raw+b']J'+s.pack('<I',(34+len(body))^key)+b'J'+s.pack('<I',len(data)^key)+b'\x86a';body+=data
index+=b'u.';header=f'RPA-3.0 {34+len(body):016x} {key:08x}\n'.encode()
put('standard.rpa','RPA',header+body+zlib.compress(index),FILES,'RPA-3, safe pickle protocol 2')
# NPA names are obfuscated even when the data encryption flag is clear.
size=sum(len(n.encode('cp932'))+21 for n,d in FILES);index=b'';body=b''
for number,(name,data)in enumerate(FILES):
 raw=name.encode('cp932');encoded=bytes((v-(252*i-6-number))&255 for i,v in enumerate(raw))
 index+=s.pack('<I',len(raw))+encoded+b'\2'+s.pack('<IIII',0,len(body),len(data),len(data));body+=data
header=b'NPA\1\0\0\0'+s.pack('<iiBBiii',2,3,0,0,len(FILES),0,len(FILES))+b'\0'*8+s.pack('<I',size)
put('standard.npa','NPA',header+index+body,FILES,'uncompressed, obfuscated CP932 names')
# YPF 0x100 uses the documented pair-swap table and an inferred name XOR key.
swap=bytes([3,0x48,6,0x35,0x0c,0x10,0x11,0x19,0x1c,0x1e,9,11,13,19,21,27,32,35,38,41,44,47,46,50])
def swaplen(n):return swap[swap.index(n)^1]if n in swap else n
size=sum(23+len(n.encode('cp932'))for n,d in FILES);index=b'';body=b''
for name,data in FILES:
 raw=name.encode('cp932');packed=zlib.compress(data)if data else data
 index+=s.pack('<IB',zlib.crc32(raw),swaplen(len(raw))^255)+bytes(v^0x42 for v in raw)+s.pack('<BBIIII',0,int(bool(data)),len(data),len(packed),32+size+len(body),zlib.crc32(data));body+=packed
put('standard.ypf','YPF',s.pack('<IIII',0x465059,0x100,len(FILES),size)+b'\0'*16+index+body,FILES,'YPF 0x100, guessed key, zlib content')
# QLIE v2: XOR-obfuscated names, unencrypted payloads, with hash index layout.
body=b''.join(d for n,d in FILES);index=b'';offset=0
for name,data in FILES:
 raw=name.encode('cp932');key=len(raw)+(0xc4^0x3e);encoded=bytes(v^((((i+1)^key)+i+1)&255)for i,v in enumerate(raw))
 index+=s.pack('<H',len(raw))+encoded+s.pack('<qIIiiI',offset,len(data),len(data),0,0,0);offset+=len(data)
footer=b'FilePackVer2.0'.ljust(16,b'\0')+s.pack('<iq',len(FILES),len(body))
put('standard.pack','PACK/QLIE',body+index+footer,FILES,'QLIE v2 raw payloads, name obfuscation')
# LWG entry names acquire the .wcg extension from the archive reader.
files=[('first.wcg',b'synthetic WCG payload'),('second.wcg',b'another payload')];index=b'';body=b''
for name,data in files:
 raw=name[:-4].encode('cp932');index+=s.pack('<iiBII',-3,4,32,len(body),len(data))+s.pack('B',len(raw))+raw;body+=data
put('standard.lwg','LWG',s.pack('<IIIIII',0x1474c,1,1,len(files),0,len(index))+index+s.pack('<I',len(body))+body,files,'raw extraction only; payloads are not encoded images')
# LIM's raw BGR565 mode; no compressor or commercial image is used.
lim=s.pack('<2sHHHIIHH',b'LM',0x12,16,0,2,1,0xf800,0x07e0)
(ROOT/'raw565.lim').write_bytes(lim)
(ROOT/'manifest.json').write_text(json.dumps({'schema':1,'provenance':'Synthetic, generated by tools/generate-fixtures.py; MIT license.','archives':manifest},ensure_ascii=False,indent=2)+'\n')
print(f'Generated {len(manifest)} archive variants and one LIM image')
