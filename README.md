# OPEN READER
Open-Source QR Code Reader

## Specification in Czech:
>Cílem programu je konzolová aplikace v jazyce C#, která na standardním vstupu dostane cestu k
obrázku/fotce QR kódu ( https://en.wikipedia.org/wiki/QR_code# ) a na standardní výstup vypíše
obsah QR kódu.
Podporovat by měla i čtení špatně vyfocených (nevyfocených přímo kolmo nebo špatně nasvícených
atd.) nebo poškozených QR kódů, což bude vyžadovat netriviální preprocessing vstupních obrázků (viz
např. https://www.atlantis-press.com/article/3264.pdf ).
Primárně by měla podporovat čtení obsahu standardních ("model 2") QR kódů v prvních deseti verzích
zakódovaného pomocí bytů (tzv. byte encoding). Program by měl být schopen pracovat se standardními
error-correction algoritmy a maskami využívanými při kódování standardních QR kódů. V případě
nedostatečného rozsahu zdrojového kódu lze implementovat i další kódování (numeric a alphanumeric)
a podporovat i vyšší verze.
Celé čtení QR kódu by mělo probíhat "v reálném čase", takže přibližně za méně než 100ms, k čemuž
by mělo dopomoci využítí více-vláknového programování. Z dalších technologií z letního semestru
bude aplikace využívat delegáty a také generické metody.
Návrh aplikace bude koncipován tak, aby co nejvíce zdrojového kódu, který by byl potenciálně
společný s čtečkami jiných 2D kódů, mohlo být v samostatném balíku následně využívaném jako
knihovna.
