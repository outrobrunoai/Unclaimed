"""Reference photos from Wikimedia Commons.
  commons.py cat  'Category:Raincoats'          list files of a category (and subcategories)
  commons.py find 'raincoat hanging'            text search
  commons.py get  prefix 'File:A.jpg' 'File:B.jpg' ...   download 1100 px copies, print licence"""
import sys, json, urllib.parse, urllib.request, time
UA = {'User-Agent': 'UnclaimedRefs/1.0 (reference study; contatobruno722@gmail.com)'}


def api(**p):
    p['format'] = 'json'
    for i in range(6):
        r = urllib.request.Request('https://commons.wikimedia.org/w/api.php?' + urllib.parse.urlencode(p), headers=UA)
        d = json.load(urllib.request.urlopen(r, timeout=30))
        if 'error' not in d:
            return d
        time.sleep(4 + 4 * i)
    raise SystemExit('commons busy')


def main():
    cmd = sys.argv[1]
    if cmd == 'cat':
        d = api(action='query', list='categorymembers', cmtitle=sys.argv[2], cmlimit=80, cmtype='file|subcat')
        for m in d['query']['categorymembers']:
            print(m['title'])
    elif cmd == 'find':
        d = api(action='query', list='search', srsearch=sys.argv[2], srnamespace=6, srlimit=25)
        for m in d['query']['search']:
            print(m['title'])
    else:
        prefix = sys.argv[2]
        for k, t in enumerate(sys.argv[3:], 1):
            d = api(action='query', titles=t, prop='imageinfo', iiprop='url|extmetadata|size', iiurlwidth=1100)
            pg = list(d['query']['pages'].values())[0]
            ii = pg['imageinfo'][0]
            fn = f'{prefix}_{k}.jpg'
            open(fn, 'wb').write(urllib.request.urlopen(urllib.request.Request(ii['thumburl'], headers=UA), timeout=60).read())
            print(fn, '|', t, '|', ii['extmetadata'].get('LicenseShortName', {}).get('value', '?'), '|', ii['descriptionurl'])
            time.sleep(1)


main()
