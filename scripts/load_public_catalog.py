"""Load the scraped JSON catalog into the running .NET API/PostgreSQL. Resume safe by source ID."""
import argparse,json,os,pathlib,time,urllib.request,urllib.error
root=pathlib.Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--api',default=os.environ.get('API_URL','http://localhost:5081'));p.add_argument('--start',type=int,default=0);args=p.parse_args()
key=os.environ['ADMIN_API_KEY'];rows=json.loads((root/'dist/movies.json').read_text())+json.loads((root/'dist/archive.json').read_text());failed=[]
for i,m in enumerate(rows):
 if i<args.start:continue
 req=urllib.request.Request(args.api+'/api/admin/movies/'+str(m['id']),data=json.dumps(m).encode(),method='PUT',headers={'Content-Type':'application/json','X-Admin-Key':key})
 try:
  with urllib.request.urlopen(req,timeout=30) as response:pass
 except (urllib.error.URLError,TimeoutError) as error:failed.append(dict(index=i,id=m['id'],error=str(error)))
 if i%100==0:print(f'{i+1}/{len(rows)} processed; failures={len(failed)}',flush=True)
(root/'import-failures.json').write_text(json.dumps(failed,indent=2));print('Finished',len(rows),'records;',len(failed),'failures.');raise SystemExit(bool(failed))
