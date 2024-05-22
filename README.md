# Florio Tools

A .NET project to read, parse, and export John Florio's 1611 Italian-English Dictionary.

My goal with this project is to provide a translation tool for early modern Italian to early modern English.
I plan to have this repo contain the code for a website, backed by a vector database containing the dictionary, and the tooling to allow anyone to clone the repo and run.

Right now it is a work in progress. Current functionality includes:

- downloading and parsing most of the dictionary entries from Project Gutenberg's "Plain Vanilla ASCII" format.
  - (there are still some edge cases to work out in the parsing)
- analysing the parsed text to identify the longest word, longest definition, and set of characters in "normalised" words
- exporting to a Microsoft Excel (.xlsx) file (mostly to assist in identifying parsing issues)

## Credits and Sources

This project benefits from some tremendous work by some amazing people who have made Florio's 1611 Italian/English Dictionary available in a variety of formats.

This project is in no way associated with the below individuals or organisations.

In particular, please consider donating to [Project Gutenberg](https://www.gutenberg.org/donate/).

### Scans

Greg Lindahl hosts images and PDFs at [https://www.pbm.com/~lindahl/florio/](https://www.pbm.com/~lindahl/florio/).

Original scans are credited to Steve Bush, and the 1968 Scholar Press facsimile.

### Project Gutenberg

Project Gutenberg's transcriptions are available at

- [HTML: https://www.gutenberg.org/cache/epub/56200/pg56200-images.html](https://www.gutenberg.org/cache/epub/56200/pg56200-images.html)
- [Text: https://www.gutenberg.org/files/56200/56200-0.txt](https://www.gutenberg.org/files/56200/56200-0.txt)

The Project Gutenberg license is available at [https://www.gutenberg.org/license](https://www.gutenberg.org/license)

Portions of the Project Gutenberg transcription are reproduced in this git repository for the purposes of documenting and testing functionality.

---

(Am I missing something to comply with the Project Gutenberg license, or could I improve how I attribute the work done by these awesome individuals and organisations? Please let me know how by getting in touch with me or opening an issue.)
