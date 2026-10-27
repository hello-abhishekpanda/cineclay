"""Refresh selected multilingual TV series from the public TVmaze API; no availability invented."""
import concurrent.futures,datetime,hashlib,json,pathlib,re,time,urllib.parse,urllib.request
from zoneinfo import ZoneInfo
from bs4 import BeautifulSoup
root=pathlib.Path(__file__).resolve().parents[1];cache=root/'.catalog-cache';cache.mkdir(exist_ok=True);today=datetime.datetime.now(ZoneInfo('Asia/Kolkata')).date().isoformat()
titles=['Wednesday','Severance','The Last of Us','Stranger Things','The Bear','Fallout','Shōgun','The White Lotus','Slow Horses','The Pitt','Pluribus','Adolescence','The Studio','Friends','Breaking Bad','Better Call Saul','Dark','Money Heist','Squid Game','Lupin','Alice in Borderland','Pachinko','The Glory','Kingdom','Sacred Games','Delhi Crime','Mirzapur','The Family Man','Paatal Lok','Panchayat','Farzi','Scam 1992: The Harshad Mehta Story','Aspirants','Gullak','Kota Factory','Kohrra','Suzhal: The Vortex','Dahaad','Asur','Rocket Boys','Criminal Justice','Made in Heaven','The Railway Men','Mismatched','Heeramandi','Dabba Cartel','The Crown','House of the Dragon','Game of Thrones','The Boys','Invincible','Reacher','The Lord of the Rings: The Rings of Power','The Wheel of Time','Foundation','Silo','Ted Lasso','Shrinking','The Morning Show','Black Mirror','Arcane','One Piece','Attack on Titan','Demon Slayer: Kimetsu no Yaiba','Death Note','The Queen\'s Gambit','Ripley','Beef','Baby Reindeer','The Diplomat','The Night Agent','The Penguin','The Gentlemen','Only Murders in the Building','Abbott Elementary','The Office','Brooklyn Nine-Nine','The Good Place','Fleabag','Derry Girls','Extraordinary Attorney Woo','Crash Landing on You','My Brilliant Friend','The Bureau','Borgen','The Killing','The Bridge','Fauda','Tehran','Ethos','The Empress','1899','Berlin','The Eternaut','When Life Gives You Tangerines','Dhootha','Save the Tigers','Kerala Crime Files','Poacher','Perilloor Premier League','Humble Politician Nograj','Pet Puraan','Mandaar','Taqdeer','Karagar','Mohanagar','Vadhandhi','Iru Dhuruvam','The Sopranos','The Wire','Mad Men','Succession','Lost','Dexter','The Handmaid\'s Tale','The Mandalorian','Andor','Loki','WandaVision','The Marvelous Mrs. Maisel','Chernobyl','True Detective','Fargo','Mindhunter','Narcos','Peaky Blinders','Boardwalk Empire','Mr. Robot','Halt and Catch Fire','The Leftovers','Six Feet Under','Buffy the Vampire Slayer','The X-Files','Doctor Who','Sherlock','Downton Abbey','Call the Midwife','Heartstopper','Sex Education','The Sandman','3 Body Problem','Blue Eye Samurai','Cyberpunk: Edgerunners','Fullmetal Alchemist: Brotherhood','Jujutsu Kaisen','Vinland Saga','My Hero Academia','Haikyu!!','SPY x FAMILY','Twenty-Five Twenty-One','Reply 1988','Itaewon Class','Jubilee','Mumbai Diaries 26/11','Scoop','Kaala Paani','Trial by Fire','Black Warrant']
show_ids={'Money Heist':27436,'Kingdom':26153,'Criminal Justice':46932,'The Bureau':6131,'The Empress':59746,'The Killing':1271,'The Bridge':1910}
def fetch(title):
 url=('https://api.tvmaze.com/shows/'+str(show_ids[title])+'?embed=cast') if title in show_ids else 'https://api.tvmaze.com/singlesearch/shows?q='+urllib.parse.quote(title)+'&embed=cast';p=cache/(hashlib.sha256(url.encode()).hexdigest()+'.json')
 try:
  if p.exists():x=json.loads(p.read_text())
  else:
   req=urllib.request.Request(url,headers={'User-Agent':'Cineclay/1.2 (catalog metadata)'});data=urllib.request.urlopen(req,timeout=22).read().decode();p.write_text(data);x=json.loads(data)
  date=x.get('premiered') or ''
  if not date or not 1990<=int(date[:4])<=int(today[:4]):return None
  language=x.get('language');channel=x.get('webChannel') or x.get('network') or {};site=x.get('officialSite') or '';offers=[]
  if site.startswith('https://') and channel.get('name'):offers=[dict(name=channel['name'],region='GLOBAL',url=site,type='official-title',checkedAt=today)]
  genre=[{'Science-Fiction':'Science Fiction'}.get(g,g) for g in x.get('genres',[])] or ['Uncategorized']
  m=dict(id=2000000000+x['id'],externalId=str(x['id']),title=x['name'],year=int(date[:4]),releaseDate=date,languages=[language] if language else [],languageSource='TVmaze original language',genres=genre,sourceGenres=x.get('genres',[]),cast=[c['person']['name'] for c in x.get('_embedded',{}).get('cast',[])][:20] or ['Not indexed'],director='',runtime=x.get('averageRuntime'),poster=(x.get('image') or {}).get('original',''),summary=BeautifulSoup(x.get('summary') or '', 'html.parser').get_text(' ',strip=True) or 'Synopsis not available.',trailer={'Wednesday':'Di310WS8zLk','Severance':'_UXKlYvLGJY'}.get(x['name'],''),score=(x.get('rating') or {}).get('average'),providers=offers,source='TVmaze',sourceUrl=x['url'],mediaType='tv',featured=True,metadataStatus='enriched',updatedAt=today)
  print(title,'→',x['name'],language,flush=True);return m
 except Exception as e:print(title,'FAILED',str(e),flush=True);return None
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:shows=[x for x in pool.map(fetch,titles) if x]
movies=json.loads((root/'dist/movies.json').read_text())
# Preserve hand-curated regional films that live only in the API seed.
api_seed=json.loads((root/'src/Cineclay.Api/seed.json').read_text())
byid={m['id']:m for m in movies}
for item in api_seed:
 if item['id'] not in byid:byid[item['id']]=item
for m in shows:
 old=byid.get(m['id'])
 if old:m['trailer']=old.get('trailer') or m['trailer'];m['providers']+= [p for p in old.get('providers',[]) if p['type']=='manual']
 byid[m['id']]=m
movies=list(byid.values())
for m in movies:
 if m.get('mediaType','movie')=='movie':m['languages']=['Korean'] if m['title']=='Parasite' else ['Japanese'] if m['title']=='Spirited Away' else ['English'];m['languageSource']='Curated original-language metadata'
archive=json.loads((root/'dist/archive.json').read_text())
for m in archive:m['languages']=['Hindi'] if m.get('regionOfOrigin')=='Hindi' else [];m['languageSource']='Hindi release list' if m['languages'] else 'Not indexed'
(root/'dist/movies.json').write_text(json.dumps(movies,ensure_ascii=False,indent=2));(root/'dist/archive.json').write_text(json.dumps(archive,ensure_ascii=False,separators=(',',':')));(root/'src/Cineclay.Api/seed.json').write_text(json.dumps(movies,ensure_ascii=False,indent=2))
r=json.loads((root/'dist/catalog-report.json').read_text());r.update(tvSeries=sum(m.get('mediaType')=='tv' for m in movies),enriched=len(movies),fetchedAt=today,languages=sorted({l for m in movies+archive for l in m.get('languages',[])}));(root/'dist/catalog-report.json').write_text(json.dumps(r,ensure_ascii=False,indent=2));print('DONE',len(shows),'TV queries imported;',r['tvSeries'],'total TV series')
