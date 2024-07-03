namespace Florio.Parsers.Gutenberg;

public class Constants
{
    public const string Close_O_Upper = "[O]";
    public const string Close_O_Lower = "[o]";
    public const string Open_O_Upper = "O";
    public const string Open_O_Lower = "o";

    public const string Close_E_Upper = "E";
    public const string Close_E_Lower = "e";
    public const string Open_E_Upper = "Ẻ";
    public const string Open_E_Lower = "ẻ";


    public const string Gutenberg_Text_Url = "https://www.gutenberg.org/cache/epub/56200/pg56200.txt";

    public const string Gutenberg_Attribution = """
        This eBook is for the use of anyone anywhere in the United States and most other parts of the world at no cost and with almost no 
        restrictions whatsoever. You may copy it, give it away or re-use it under the terms of the Project Gutenberg License included with this 
        eBook or online at www.gutenberg.org. If you are not located in the United States, you will have to check the laws of the country where 
        you are located before using this eBook.
        """;

    public const string Gutenberg_Transcribers_Note = """
        Transcriber's Note

        Throughout the Dictionary two different forms of the letters E and O are used, to represent the different
        sounds they can have in Italian. The close E is displayed in its normal form (E — e), the open E with a special
        character the author had made for this very purpose: it has here been rendered with Ẻ — ẻ. The close O has an oval
        shape, and has been represented in this text version with [O] [o], while the open O has its normal appearance.
        """;
}
