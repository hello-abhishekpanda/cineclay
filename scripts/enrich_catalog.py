"""Enrich selected archive films from Wikimedia summary endpoints; keep per-record attribution."""
import concurrent.futures,copy,json,pathlib,urllib.request,urllib.parse,hashlib
root=pathlib.Path(__file__).resolve().parents[1]
archive=json.loads((root/'dist/archive.json').read_text());enriched=json.loads((root/'dist/movies.json').read_text())
names=['Project Hail Mary','The Odyssey','Spider-Man: Brand New Day','Toy Story 5','The Super Mario Galaxy Movie','Michael','Obsession','The Devil Wears Prada 2','Backrooms','Minions & Monsters']
rows=[]
for name in names:
 match=next((m for m in archive+enriched if m['year']==2026 and m['title'].casefold()==name.casefold()),None)
 if match:rows.append(copy.deepcopy(match))
def enrich(m):
 try:
  slug=urllib.parse.urlparse(m.get('sourceUrl','')).path.split('/wiki/')[-1] if m.get('sourceUrl') else 'Project_Hail_Mary_(film)'
  url='https://en.wikipedia.org/api/rest_v1/page/summary/'+slug
  req=urllib.request.Request(url,headers={'User-Agent':'CineclayCatalog/1.1'})
  data=json.load(urllib.request.urlopen(req,timeout=25));m['summary']=data.get('extract') or m['summary'];img=data.get('thumbnail',{}).get('source') or data.get('originalimage',{}).get('source')
  if img:
   r=urllib.request.urlopen(urllib.request.Request(img,headers={'User-Agent':'CineclayCatalog/1.1'}),timeout=25);b=r.read()
   if r.headers.get('Content-Type','').startswith('image/'):
    path='posters/wiki-'+str(m['id'])+'.jpg';(root/'dist'/path).write_bytes(b);m['poster']='/'+path
  m.update(metadataStatus='enriched',source='Wikipedia',sourceUrl=data.get('content_urls',{}).get('desktop',{}).get('page') or m.get('sourceUrl'),sourceLicense='CC BY-SA 4.0',updatedAt='2026-10-26')
  # A theatrical poster is enrichment, not a streaming-availability claim.
  print(m['title'],bool(m['poster']),flush=True);return m
 except Exception as e:print(m['title'],str(e),flush=True);return None
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:results=[m for m in pool.map(enrich,rows) if m]
for m in results:
 old=next((x for x in enriched if x['title']==m['title'] and x['year']==m['year']),None)
 if old:m['id']=old['id'];m['providers']=old['providers'];enriched=[x for x in enriched if x['id']!=old['id']]
 enriched.append(m)
keys={(m['title'].casefold(),m['year']) for m in enriched};archive=[m for m in archive if (m['title'].casefold(),m['year']) not in keys]
(root/'dist/movies.json').write_text(json.dumps(enriched,ensure_ascii=False,indent=2));(root/'dist/archive.json').write_text(json.dumps(archive,ensure_ascii=False,separators=(',',':')))
r=json.loads((root/'dist/catalog-report.json').read_text());r.update(movies=sum(m.get('mediaType','movie')=='movie' for m in enriched)+len(archive),tvSeries=sum(m.get('mediaType')=='tv' for m in enriched),enriched=len(enriched),indexOnly=len(archive));(root/'dist/catalog-report.json').write_text(json.dumps(r,ensure_ascii=False,indent=2))
