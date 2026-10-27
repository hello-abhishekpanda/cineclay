"""Run against a disposable local API database. Soft-deletes its test record."""
import json,os,urllib.request,urllib.error,time
base=os.environ.get('API_URL','http://localhost:5081')
key=os.environ['ADMIN_API_KEY']
def request(method,path,data=None,auth=True):
 headers={'Content-Type':'application/json'}
 if auth:headers['X-Admin-Key']=key
 req=urllib.request.Request(base+path,data=None if data is None else json.dumps(data).encode(),headers=headers,method=method)
 try:
  with urllib.request.urlopen(req,timeout=30) as r:
   payload=r.read();return r.status,json.loads(payload) if payload else None
 except urllib.error.HTTPError as e:return e.code,None
id=int(time.time()*1000)
movie=dict(id=id,title='Cineclay API smoke test',year=2026,genres=['Drama'],cast=['Test actor','Test actress'],director='Test director',summary='Disposable integration-test record.',poster='',trailer='',providers=[])
assert request('GET','/health/ready')[0]==200
assert request('PUT',f'/api/admin/movies/{id}',movie,False)[0]==401
assert request('PUT',f'/api/admin/movies/{id}',movie)[0]==200
assert request('GET',f'/api/movies/{id}')[1]['title']==movie['title']
assert request('GET','/api/movies?q=Cineclay%20API%20smoke&year=2026&genre=Drama')[1]['total']>=1
owner='00000000-0000-0000-0000-000000000001'
assert request('PUT',f'/api/admin/watchlist/{owner}/{id}?saved=true')[0]==204
assert id in request('GET',f'/api/admin/watchlist/{owner}')[1]
assert request('POST',f'/api/admin/reviews/{owner}/{id}',{'rating':6,'text':'invalid'})[0]==400
assert request('POST',f'/api/admin/reviews/{owner}/{id}',{'rating':5,'text':'A useful test.'})[0]==204
assert request('GET',f'/api/admin/reviews/{owner}/{id}')[1][0]['rating']==5
movie['year']=1989
assert request('PUT',f'/api/admin/movies/{id}',movie)[0]==400
movie['year']=2026;movie['summary']='Updated test record.'
assert request('PUT',f'/api/admin/movies/{id}',movie)[0]==200
assert request('GET',f'/api/movies/{id}')[1]['summary']=='Updated test record.'
assert request('DELETE',f'/api/admin/movies/{id}')[0]==204
assert request('GET',f'/api/movies/{id}')[0]==404
print('API smoke checks passed: authentication, CRUD, filters, watchlists, reviews and validation.')
