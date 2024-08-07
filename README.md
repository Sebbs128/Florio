# Florio Tools

A .NET project to read, parse, and export John Florio's 1611 Italian-English Dictionary.

My goal with this project is to provide a translation tool for early modern Italian to early modern English.
Within this repo you will find the code for a website backed by a vector database containing the dictionary, and the tooling to allow anyone to clone the repo and run the site.

Notable executables within the repo:

- `Florio.WebApp`
  - an ASP.NET Core website, giving the ability to search a vector database for Italian words contained in Florio's dictionary
- `Florio.AppHost`
  - a .NET Aspire application host which handles orchestrating a Qdrant vector database, the `Florio.WebApp` site, and `Florio.VectorDbManager` to automatically populate the vector database
- `Florio.Exporters.Excel`
  - exports the parsed e-book as a Microsoft Excel (.xlsx) file (mostly to assist in identifying parsing issues)
- `Florio.Exporters.Onnx`
  - exports an ONNX model for the vector embeddings of Italian words from the parsed e-book
    - this .onnx file should be copied to the Embeddings/ModelFiles/ folder in Florio.Utilities.VectorTesting and Florio.WebApp before running them
- `Florio.Utilities.ConsoleTest`
  - downloads and parses most of the dictionary entries from Project Gutenberg's "Plain Text UTF-8" e-book
  - analyses the parsed text to identify some features
    - the longest word,
    - the longest definition,
    - set of characters in "normalised" words that will be used for tri-gram vectors,
    - any issues with word variations (often grammatical conjugations) that should be addressed in the parser
- `Florio.Utilities.VectorTesting`
  - tests looking up words from an in-memory vector database

## Running the solution

First, the vector embeddings model should be generated. This is done by running `Florio.Exporters.Onnx`, which will create a `word-embeddings.onnx` in that project folder.

> Note: if you don't want to repeatedly hit Project Gutenberg's site, the .txt file can be downloaded and saved.
> Projects will look for a .txt file named `pg56200.txt` in a `.localassets` by default (I recommend creating this directory in the root folder of the solution), but this path can be changed in the calls to `AddGutenbergDownloaderAndParser()`.

The generated `word-embeddings.onnx` should be copied to the `Embeddings/ModelFiles/` folders in `Florio.Utilities.VectorTesting`, `Florio.VectorDbManager`, and `Florio.WebApp` before running the VectorTesting utility, or `Florio.AppHost`.

The solution uses .NET Aspire to simultaneously launch a Qdrant docker container, the VectorDbManager project to initialise and populate the vector database, and the WebApp. The recommended way to run the site is to start `Florio.AppHost`, which will ensure everything needed is running.

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
