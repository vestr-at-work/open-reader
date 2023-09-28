# 📖 Open Reader
Open-Source QR Code Reader written and availabe for use in C#.

## User documentation
As of right now the project is showcased in an CLI demo application.
The basic use consists of cloning the project and building the **CodeReaderCommons** library by executing the following command in the `/CodeReaderCommons` directory. 
```shell
dotnet build
``` 
Then finally running the following command in the `/OpenReader` directory with the path to the input image as a first argument.
```shell
dotnet run "path/to/your/input/image"
```
If decoding is successful program will print the following.
```console
Result: "Content of the QR Code."
```
If decoding fails program will print one of the following depending on the situation that occured.
```console
Error: Need path to the QR code image as a first position argument.
```

```console
Error: Could not recognize QR code symbol in the image.
```

```console
Error: Could not load format info from the QR code symbol. Possibly too corrupted image.
```

```console
Error: Could not load QR code data properly. Possibly too corrupted image.
```

>Remarks:
>Only QR codes **up to Version 16 and in the byte encoding are supported**. This will be expanded in the future.

## Specification in Czech
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

## Diagram of the decoding process
![Diagram of the decoding process](./DecodingStepsDiagram.drawio.svg)
