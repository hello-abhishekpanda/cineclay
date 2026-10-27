"""Normalize source genre terms and attach known official trailer references; never fabricate missing fields."""
import json,pathlib
root=pathlib.Path(__file__).resolve().parents[1]
allowed={'Action','Adventure','Animation','Biography','Comedy','Crime','Documentary','Drama','Family','Fantasy','History','Horror','Music','Mystery','Romance','Science Fiction','Sport','Thriller','War','Western'}
terms=[(['action'],'Action'),(['adventure'],'Adventure'),(['animat'],'Animation'),(['biograph'],'Biography'),(['comedy'],'Comedy'),(['crime'],'Crime'),(['documentary'],'Documentary'),(['drama'],'Drama'),(['family'],'Family'),(['fantasy'],'Fantasy'),(['histor','period'],'History'),(['horror'],'Horror'),(['music'],'Music'),(['mystery'],'Mystery'),(['romance','romantic'],'Romance'),(['sci-fi','sci fi','science fiction'],'Science Fiction'),(['sport'],'Sport'),(['thriller','suspense'],'Thriller'),(['war'],'War'),(['western'],'Western')]
for name in ['movies','archive']:
 path=root/'dist'/f'{name}.json';rows=json.loads(path.read_text())
 for m in rows:
  raw=m.get('sourceGenres') or m.get('genres') or [];m['sourceGenres']=raw;text=' '.join(raw).lower();values=[g for words,g in terms if any(word in text for word in words)]
  if not values and m.get('metadataStatus')=='enriched' and m.get('source')=='Wikipedia':
   text=m.get('summary','').lower()[:220];values=[g for words,g in terms if any(word in text for word in words)];m['genreSource']='classified from attributed summary'
  m['genres']=values or ['Uncategorized']
  if m['title']=='Project Hail Mary':m['trailer']='m08TxIsFTRI'
  if m['title']=='The Odyssey' and m['year']==2026:m['trailer']='f_bKjZeJBBI';m['trailerSource']='https://www.nbcuniversal.com/article/christopher-nolans-odyssey-unveils-new-trailer-first-film-shot-entirely-imax-cameras'
 path.write_text(json.dumps(rows,ensure_ascii=False,indent=2 if name=='movies' else None))
(root/'src/Cineclay.Api/seed.json').write_text((root/'dist/movies.json').read_text())
