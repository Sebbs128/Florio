# Florio Tools

A .NET project to read, parse, and export John Florio's 1611 Italian-English Dictionary.

My goal with this project is to provide a translation tool for early modern Italian to early modern English.
Within this repo you will find the code for a website backed by a vector database containing the dictionary, and the tooling to allow anyone to clone the repo and run the site.

Notable executables within the repo:

- Florio.WebApp
  - an ASP.NET Core website, giving the ability to search a vector database for Italian words contained in Florio's dictionary
- Florio.AppHost
  - a .NET Aspire application host which handles orchestrating a Qdrant vector database along with the Florio.WebApp site
- Florio.Exporters.Excel
  - exports the parsed e-book as a Microsoft Excel (.xlsx) file (mostly to assist in identifying parsing issues)
- Florio.Utilities.ConsoleTest
  - downloads and parses most of the dictionary entries from Project Gutenberg's "Plain Text UTF-8" e-book
  - analyses the parsed text to identify some features
    - the longest word,
    - the longest definition,
    - set of characters in "normalised" words that will be used for tri-gram vectors,
    - any issues with word variations (often grammatical conjugations) that should be addressed in the parser
- Florio.Utilities.VectorTesting
  - exports an ONNX (.onnx) file containing a vector embeddings model fitted from the normalised words as tri-grams, and tests looking up words from an in-memory vector database.

## Credits and Sources

This project benefits from some tremendous work by some amazing people who have made Florio's 1611 Italian/English Dictionary available in a variety of formats.

This project is in no way associated with the below individuals or organisations.

In particular, please consider donating to [Project Gutenberg](https://www.gutenberg.org/donate/).

### Scans

Greg Lindahl hosts images and PDFs at [https://www.pbm.com/~lindahl/florio/](https://www.pbm.com/~lindahl/florio/).

Original scans are credited to Steve Bush, and the 1968 Scholar Press facsimile.

### Project Gutenberg

Project Gutenberg's transcriptions are available at (https://www.gutenberg.org/ebooks/56200).

The Project Gutenberg license is available at [https://www.gutenberg.org/license](https://www.gutenberg.org/license).

Project Gutenberg credits production of their transcriptions to Greg Lindahl, Steve Bush, Barbara Magni and the Online Distributed Proofreading Team at (http://www.pgdp.net).

Portions of the Project Gutenberg transcription are reproduced in this git repository for the purposes of documenting and testing functionality.

---

(Am I missing something to comply with the Project Gutenberg license, or could I improve how I attribute the work done by these awesome individuals and organisations? Please let me know how by getting in touch with me or opening an issue.)
