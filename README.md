# Cube
Jednotlivé tahy lze učinit stiknutím pravého tlačítka a táhnutím ve směru rotace (v kodu je nastavena
potřebná míra pohybu ke spuštění rotace). Rotací může přobíhat více současně pouze ve stejném směru,
tedy pokud dojde k požadavku na rotaci v jiném směru než je probíhající rotace, program ji bude ignorovat.

Dále je možné nastavit velikost kostky parametrem "size" reprezentující počet kostiček v jedné linii.
Prametr "renderAllCubes" určuje zda má být renderován i vnitřek kostky( kvůli optimalizaci).
U kostky lze dále zapnout náhodné promíchávání pomocí paramteru "shuffle", které vždy vybere jednu až 'n'
vrstev a otočí v náhodném směru.

Kostce je možné zapnout náhodné generování barev, které každé straně kostky přiřadí
náhodou barvu, která se liší od barev ostatních stran. Tato možnost lze zapnout parametrem
"randomColor".

Poslední parameter "specialAnimation" nastaví jinou animaci rotace.
