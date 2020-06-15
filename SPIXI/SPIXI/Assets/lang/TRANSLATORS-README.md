# Instructions and notes for Spixi translators :

1. Reference language file is en-us.txt
2. Language files must be saved as 'UTF-8 with BOM' text file. It's best if you copy en-us.txt to a new file and start translating there
3. Language files use a simple key = value approach
4. Make sure to translate only values, keys must NEVER be translated
5. lines that start with a semicolon (;) are comments/notes and should not be translated
6. &lt;br> or &lt;br/> or &lt;br /> means new line
7. {0}, {1}, {2}, ... means parameters will be passed in by Spixi - every translation must ALWAYS have the same number of parameters as the original text, they can be placed anywhere in the text (where it makes sense)
8. never use double quotes (") in translated texts, use '&amp;quot;' instead

Languages           |  Translating    |  Available
--- | --- | --- |
Afrikaans           |  ❌            |  ❌
Arabian             |  ❌            |  ❌
Chinese             |  ❌            |  ❌
Croatian            |  ✅            |  ❌
English ( default ) |  ✅            |  ✅
French              |  ✅            |  ❌
German              |  ✅            |  ❌
Italian             |  ❌            |  ❌
Japanese            |  ❌            |  ❌
Korean              |  ❌            |  ❌
Portuguese          |  ❌            |  ❌
Romanian            |  ✅            |  ❌
Serbian             |  ✅            |  ❌
Slovenian           |  ✅            |  ❌
Spanish             |  ✅            |  ❌