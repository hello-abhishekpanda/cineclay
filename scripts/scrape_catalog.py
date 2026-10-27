"""Public Wikipedia year-list scraper + public TVmaze enrichment. Resume-safe cache; no login or bypass."""
import argparse,concurrent.futures,datetime,hashlib,json,pathlib,re,time,urllib.request,urllib.error
from bs4 import BeautifulSoup
ROOT=pathlib.Path(__file__).resolve().parents[1];CACHE=ROOT/'.catalog-cache';CACHE.mkdir(exist_ok=True)
from zoneinfo import ZoneInfo
TODAY=datetime.datetime.now(ZoneInfo("Asia/Kolkata")).date()
def get(url):
 p=CACHE/(hashlib.sha256(url.encode()).hexdigest()+'.json')
 if p.exists():return p.read_text()
 req=urllib.request.Request(url,headers={'User-Agent':'CineclayCatalog/1.1 (public film bibliography; no authentication)'})
 with urllib.request.urlopen(req,timeout=35) as r:s=r.read().decode()
 p.write_text(s);return s

def clean(node):
 if node is None:return ''
 return re.sub(r'\[\s*\d+\s*\]','',node.get_text(' ',strip=True)).strip()

def scrape(task):
 country,year=task;url=f'https://en.wikipedia.org/wiki/List_of_{country}_films_of_{year}'
 try:
  soup=BeautifulSoup(get(url),'html.parser');out={}
  for table in soup.select('table.wikitable'):
   trs=table.select('tr');heads=[clean(x).lower() for x in trs[0].find_all(['th','td'],recursive=False) for _ in range(int(x.get('colspan',1)))] if trs else []
   # Build a logical grid to respect release-date rowspans and title columns.
   if 'title' not in heads or not any(x in heads for x in ['genre','director','cast and crew','cast']):continue
   titleindex=heads.index('title');spans={}
   for tr in trs[1:]:
    cells={};newspans={};idx=0
    for col,(node,left) in spans.items():
     cells[col]=node
     if left>1:newspans[col]=(node,left-1)
    for td in tr.find_all(['th','td'],recursive=False):
     while idx in cells:idx+=1
     width=int(td.get('colspan',1));height=int(td.get('rowspan',1))
     for col in range(idx,idx+width):
      cells[col]=td
      if height>1:newspans[col]=(td,height-1)
     idx+=width
    spans=newspans
    td=cells.get(titleindex)
    if td is None:continue
    link=td.select_one('i a[href]') or td.select_one('a[href]')
    if link is None:continue
    title=clean(td);slug=urllib.parse.urlparse(urllib.parse.urljoin(url,link.get('href',''))).path;
    if not slug.startswith('/wiki/'):continue
    if year==TODAY.year and 'opening' in heads:
     monthcell=cells.get(0);daycell=cells.get(1);month=int(monthcell.get('data-sort-value','0')) if monthcell else 0
     daytext=clean(daycell) if daycell else '';day=int(daytext) if daytext.isdigit() else 0
     if month>TODAY.month or month==TODAY.month and day>TODAY.day:continue
    
    if not title or len(title)>200 or ':' in slug.split('/wiki/')[-1]:continue
    # Current-year lists can include announced future titles. Keep them as announced, not released claims.
    fields={h:clean(cells[i]) for i,h in enumerate(heads) if i in cells}
    genre=[x.strip().title() for x in re.split(r',|/',fields.get('genre','')) if x.strip()]
    cast=fields.get('cast','')
    castlist=[x.strip() for x in cast.split(',') if x.strip()] if cast else []
    if not castlist and 'cast and crew' in fields:
     castlist=[clean(a) for a in cells[heads.index('cast and crew')].select('a[rel="mw:WikiLink"]')][-20:]
    stable=int(hashlib.sha256(slug.encode()).hexdigest()[:12],16)+1000000000000
    out[slug]=dict(id=stable,title=title,year=year,genres=genre or ['Uncategorized'],cast=castlist or ['Not indexed'],director=fields.get('director',''),runtime=None,poster='',summary='Synopsis has not been imported for this archive record.',trailer='',score=None,providers=[],source='Wikipedia',sourceUrl='https://en.wikipedia.org'+slug,mediaType='movie',releaseDate=None,updatedAt=TODAY.isoformat(),featured=False,metadataStatus='index-only',regionOfOrigin=country)
  return list(out.values()),dict(source=url,count=len(out),status='ok')
 except Exception as e:return [],dict(source=url,count=0,status=type(e).__name__,error=str(e))

def television():
 titles=['Wednesday','Severance','The Last of Us','Stranger Things','The Bear','Fallout','Shōgun','The White Lotus','Slow Horses','The Pitt','Pluribus','Adolescence','The Studio','Friends','Breaking Bad']
 out=[];sources=[]
 for title in titles:
  url='https://api.tvmaze.com/singlesearch/shows?q='+urllib.parse.quote(title)+'&embed=cast'
  try:
   x=json.loads(get(url));date=x.get('premiered') or ''
   if not date or not 1990<=int(date[:4])<=TODAY.year:continue
   poster=(x.get('image') or {}).get('original','');web=x.get('webChannel') or {};site=x.get('officialSite') or '';platform=web.get('name','');p=[]
   # Official title sites are recorded as such; not asserted to be region-verified streaming offers.
   if site.startswith('https://') and platform:p=[dict(name=platform,region='GLOBAL',url=site,type='official-title',checkedAt=TODAY.isoformat())]
   out.append(dict(id=2000000000+x['id'],externalId=str(x['id']),title=x['name'],year=int(date[:4]),releaseDate=date,genres=x.get('genres') or ['Uncategorized'],cast=[c['person']['name'] for c in x.get('_embedded',{}).get('cast',[])][:20] or ['Not indexed'],director='',runtime=x.get('averageRuntime'),poster=poster,summary=BeautifulSoup(x.get('summary') or '', 'html.parser').get_text(' ',strip=True) or 'Synopsis not available.',trailer={'Wednesday':'Di310WS8zLk','Severance':'_UXKlYvLGJY'}.get(title,''),score=(x.get('rating') or {}).get('average'),providers=p,source='TVmaze',sourceUrl=x['url'],mediaType='tv',featured=True,metadataStatus='enriched',updatedAt=TODAY.isoformat()))
   sources.append(dict(source=url,count=1,status='ok'))
  except Exception as e:sources.append(dict(source=url,count=0,status='error',error=str(e)))
  time.sleep(.25)
 return out,sources

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--from-year',type=int,default=1990);parser.add_argument('--to-year',type=int,default=TODAY.year);parser.add_argument('--country',choices=['American','Hindi','both'],default='both');parser.add_argument('--tv-only',action='store_true');args=parser.parse_args()
 countries=['American','Hindi'] if args.country=='both' else [args.country]
 tasks=[] if args.tv_only else [(c,y) for y in range(args.from_year,args.to_year+1) for c in countries]
 allrows=[];reports=[]
 with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
  for rows,report in pool.map(scrape,tasks):allrows.extend(rows);reports.append(report);print(json.dumps(report),flush=True)
 tv,ts=television();reports+=ts
 originals=json.loads((ROOT/'dist/movies.json').read_text())
 originals=[m for m in originals if m.get('source')!='TVmaze']
 for m in originals:m.update(mediaType='movie',metadataStatus='enriched')
 names={(m['title'].casefold(),m['year']) for m in originals}
 archive=list({m['sourceUrl']:m for m in allrows if (m['title'].casefold(),m['year']) not in names}.values())
 (ROOT/'dist/archive.json').write_text(json.dumps(archive,ensure_ascii=False,separators=(',',':')))
 (ROOT/'dist/movies.json').write_text(json.dumps(originals+tv,ensure_ascii=False,indent=2))
 report=dict(fetchedAt=TODAY.isoformat(),scope='Wikipedia American and Hindi release-year lists; selected TVmaze series. Not a complete worldwide catalog.',movies=len(originals)+len(archive),tvSeries=len(tv),indexOnly=len(archive),enriched=len(originals)+len(tv),sources=reports)
 (ROOT/'dist/catalog-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
 print('DONE',json.dumps({k:v for k,v in report.items() if k!='sources'}))
if __name__=='__main__':main()
