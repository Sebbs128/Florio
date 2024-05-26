namespace Florio.Gutenberg.Parser.Tests
{
    public class GutenbergTextParserTests
    {
        [Fact]
        public void IsEndOfDefinitions_IsTrue_WhenLineIsFinis()
        {
            string line = "                                 FINIS.";
            Assert.True(GutenbergTextParser.IsEndOfDefinitions(line));
        }

        [Theory]
        [MemberData(nameof(DefinitionLines))]
        public void CanParseLineIntoDefinitionLine(string line, WordDefinition definition)
        {
            Assert.True(GutenbergTextParser.ContainsDefinition(line));
            Assert.Equal(definition, GutenbergTextParser.GetDefinitionLine(line));
        }

        [Theory]
        [MemberData(nameof(WordVariations))]
        public void CanExtractWordVariations(string wordWithVariations, IEnumerable<string> variations)
        {
            Assert.Equal(variations, GutenbergTextParser.GetVariations(wordWithVariations));
        }

        [Theory]
        [MemberData(nameof(DefinitionsWithReferences))]
        public void CanRetrieveReferencedWords(string definition, IEnumerable<string> referencedWords)
        {
            Assert.Equal(referencedWords, GutenbergTextParser.GetReferencedWords(definition));
        }

        [Theory]
        [MemberData(nameof(LineCombinations))]
        public async Task ParseLines_LineCombinations(string input, IEnumerable<WordDefinition> expected)
        {
            var downloader = new FakeDownloader(input);
            var parser = new GutenbergTextParser(downloader);

            var actual = await parser.ParseLines().ToListAsync();
            Assert.Equal(expected, actual);
        }

        public static TheoryData<string, WordDefinition> DefinitionLines => new()
        {
            {
                // line
                "A, _The first letter of the alphabet, and the first vowell._",
                // definition
                new WordDefinition("A", "_The first letter of the alphabet, and the first vowell._")
            },
            {
                "Pr<i>ò</i> Pr<i>ò</i>, _much much good may it doe you, well may you fare._",
                new WordDefinition("Pr[ò] Pr[ò]", "_much much good may it doe you, well may you fare._")
            }
        };

        public static TheoryData<string, List<string>> WordVariations => new()
        {
            {
                "Abachísta",
                new List<string>
                {
                    "Abachísta"
                }
            },
            {
                "Apẻndi[o], Apẻnd[o]",
                new List<string>()
                {
                    "Apẻndi[o]",
                    "Apẻnd[o]"
                }
            },
            {
                "Affielíre, lísc[o], lít[o]",
                new List<string>()
                {
                    "Affielíre",
                    "Affielísc[o]",
                    "Affielít[o]"
                }
            },
            {
                "Affieníre, nísc[o], nít[o]",
                new List<string>()
                {
                    "Affieníre",
                    "Affienísc[o]",
                    "Affienít[o]",
                }
            },
            {
                "Affieu[o]líre, lísc[o], lít[o]",
                new List<string>()
                {
                    "Affieu[o]líre",
                    "Affieu[o]lísc[o]",
                    "Affieu[o]lít[o]",
                }
            },
            {
                "Affíggere, fígg[o], físsi, físs[o] _or_ fítt[o]",
                new List<string>()
                {
                    "Affíggere",
                    "Affígg[o]",
                    "Affíssi",
                    "Affíss[o]",
                    "Affítt[o]",
                }
            },
                {
                    "Dissegnáre, &c.",
                    new List<string>()
                    {
                        "Dissegnáre, &c."
                    }
                },
                {
                    "Distrigáre, &c.",
                    new List<string>()
                    {
                        "Distrigáre, &c."
                    }
                },
                {
                    "Fémmina, &c.",
                    new List<string>()
                    {
                        "Fémmina, &c."
                    }
                },
                {
                    "Sẻttezz[ó]ni, p[ó]nti, c[o]lisẻi, acqued[ó]tti, & sẻttezz[ó]ni",
                    new List<string>()
                    {
                        "Sẻttezz[ó]ni, p[ó]nti, c[o]lisẻi, acqued[ó]tti, & sẻttezz[ó]ni"
                    }
                },
                {
                    "Torpẻnte, quási pígro, & oti[ó]s[o]",
                    new List<string>()
                    {
                        "Torpẻnte, quási pígro, & oti[ó]s[o]"
                    }
            },
        };

        public static TheoryData<string, IEnumerable<string>> DefinitionsWithReferences => new()
        {
            {
                "_as_ Abbacáre.",
                [ "Abbacáre" ]
            },
            {
                "_as_ A sácc[o].",
                [ "A sácc[o]" ]
            },
            {
                "_as_ Arctúr[o]. _Also the Clot-bur._",
                [ "Arctúr[o]" ]
            },
            {
                "_as_ Arecáre, _to reach vnto._",
                [ "Arecáre" ]
            },
            {
                "_as_ Athẻísta, _or_ Athẻ[o].",
                [ "Athẻísta", "Athẻ[o]" ]
            },
            {
                "_as_ Mascalz[ó]ne, _as_ Bárr[o].",
                [ "Mascalz[ó]ne", "Bárr[o]" ]
            },
            {
                "_vsed for_ Biéc[o], _as_ Biócc[o]li.",
                [ "Biéc[o]", "Biócc[o]li" ]
            },
            {
                "_as_ Bótta, Di bótt[o], _quickly. Also a stroke._",
                [ "Bótta, Di bótt[o]" ]
            },
            {
                "_as_ Fẻccia, _or as_ Gr[ó]mma.",
                [ "Fẻccia", "Gr[ó]mma" ]
            },
            {
                "_as_ Cauagliẻre, &c.",
                [ "Cauagliẻre" ]
            }
        };

        public static TheoryData<WordDefinition, WordDefinition, WordDefinition> ReferencedWordDefinitions => new()
        {
            {
                new WordDefinition("Abacáre", "_as_ Abbacáre."),
                new WordDefinition("Abbacáre", "_to number or cast account, also to prie into or seeke out with diligence._"),
                new WordDefinition("Abacáre", "_to number or cast account, also to prie into or seeke out with diligence._")
            },
            {
                new WordDefinition("Abbẻlláre", "_as_ Abbẻllíre, _also to sooth vp, or please ones mind._"),
                new WordDefinition("Abbẻllíre", "_to embellish, to beautifie, to decke, to adorne, to decore, to make faire._"),
                new WordDefinition("Abbẻlláre", "_to embellish, to beautifie, to decke, to adorne, to decore, to make faire. also to sooth vp, or please ones mind._")
            }
        };

        public static TheoryData<string, IEnumerable<WordDefinition>> LineCombinations => new()
        {
            // just a page heading
            {
                // input
                "A",
                // expected output
                new List<WordDefinition>()
            },
            // a sentence appearing before any page headings
            {
                "_A most copious and exact Dictionarie in_ Italian and English.",
                new List<WordDefinition>()
            },
            // lines either side of the first page heading (including the page heading)
            {
                """
                                
                _A most copious and exact Dictionarie in_ Italian and English.




                A


                A, _The first letter of the alphabet, and the first vowell._
                """,
                new List<WordDefinition>
                {
                    new("A", "_The first letter of the alphabet, and the first vowell._")
                }
            },
            // the text after the last definition
            {
                "                                 FINIS.",
                new List<WordDefinition>()
            },
            // a line before the first page heading, the page heading, some specific definitions for test cases, and some lines after the finish
            {
                """
                                
                _A most copious and exact Dictionarie in_ Italian and English.




                A


                A, _The first letter of the alphabet, and the first vowell._

                A, _a preposition or sign of the Datiue case, to, vnto, at, at the, to
                the._

                A, _a preposition or signe of the ablatiue case, namely comming after
                verbes of priuation, as_ Tógliere, Rubbáre, _&c. from, from of, of._

                Abacchiére, _a caster of accounts._

                Abachísta, _idem._

                Apẻndi[o], Apẻnd[o], _downe-hanging._
                
                Affluíre, ísc[o], ít[o], _to flow vnto. Also to abound in wealth._

                Dissegnáre, &c. _as_ Disegnáre.

                Distrigáre, &c. _as_ Districáre.

                Fémmina, &c. _as_ Fémina.

                Máglia degl'ócchij, _a pin and web or other spots in the eies._

                  Máglia lárga.    }
                                   }
                  Máglia l[ó]nga.  }
                                   } Certain net-worke
                  Máglia quádra.   } so called of Semsters.
                                   }
                  Máglia strétta.  }
                                   }
                  Máglia t[ó]nda.  }

                Prẻzzáre,_ as_ Prẻgiáre, _to bargane or make price for any thing._

                Pr<i>ò</i> Pr<i>ò</i>, _much much good may it doe you, well may you fare._

                Rint[er]rzáta cárta, _a bun-carde. Also a carde prickt or packt for aduantage._

                R[o]mpicóll[o],_ a breake-necke place, a downefal, a headlong
                precipice,_ A r[o] mpicóll[o], _headlong, rashly, desperately, in danger
                of breaking ones necke.Also a desperate, rash or heedlesse fellow._

                Sẻttezz[ó]ni, p[ó]nti, c[o]lisẻi, acqued[ó]tti, & sẻttezz[ó]ni, _a kind
                of proud fabrike._

                Torpẻnte, quási pígro, & oti[ó]s[o], _dull, heauy, benummed, clumsie,
                sluggish._

                Vliuígn[o], of forme or colour of an oliue.

                Xisti[ó]ne, _a kind of precious stone._

                Xist[ó]ne, _a place of exercise in faire weather, a wrestling-place._


                                                 FINIS.




                                               NECESSARY
                                            RVLES AND SHORT
                                          OBSERVATIONS FOR THE
                                          TRVE PRONOVNCING AND
                                          SPEEDIE LEARNING OF
                                          The Italian Tongue.
                """,
                new List<WordDefinition>
                {
                    new("A", "_The first letter of the alphabet, and the first vowell._"),
                    new("A", "_a preposition or sign of the Datiue case, to, vnto, at, at the, to the._"),
                    new("A", "_a preposition or signe of the ablatiue case, namely comming after verbes of priuation, as_ Tógliere, Rubbáre, _&c. from, from of, of._"),
                    new("Abacchiére", "_a caster of accounts._"),
                    new("Abachísta", "_a caster of accounts._"),
                    new("Apẻndi[o]", "_downe-hanging._"),
                    new("Apẻnd[o]", "_downe-hanging._"),
                    new("Affluíre", "_to flow vnto. Also to abound in wealth._"),
                    new("Affluísc[o]", "_to flow vnto. Also to abound in wealth._"),
                    new("Affluít[o]", "_to flow vnto. Also to abound in wealth._"),
                    new("Dissegnáre, &c.", "_as_ Disegnáre."),
                    new("Distrigáre, &c.", "_as_ Districáre."),
                    new("Fémmina, &c.", "_as_ Fémina."),
                    new("Máglia degl'ócchij",
                        """
                        _a pin and web or other spots in the eies._
                        
                          Máglia lárga.    }
                                           }
                          Máglia l[ó]nga.  }
                                           } Certain net-worke
                          Máglia quádra.   } so called of Semsters.
                                           }
                          Máglia strétta.  }
                                           }
                          Máglia t[ó]nda.  }
                        """),
                    new("Prẻzzáre", "_ as_ Prẻgiáre, _to bargane or make price for any thing._") { ReferencedWords = [ "Prẻgiáre" ]},
                    new("Pr[ò] Pr[ò]", "_much much good may it doe you, well may you fare._"),
                    new("Rinterrzáta cárta", "_a bun-carde. Also a carde prickt or packt for aduantage._"),
                    new("R[o]mpicóll[o]", "_ a breake-necke place, a downefal, a headlong precipice,_ A r[o] mpicóll[o], _headlong, rashly, desperately, in danger of breaking ones necke.Also a desperate, rash or heedlesse fellow._"),
                    new("Sẻttezz[ó]ni, p[ó]nti, c[o]lisẻi, acqued[ó]tti, & sẻttezz[ó]ni", "_a kind of proud fabrike._"),
                    new("Torpẻnte, quási pígro, & oti[ó]s[o]", "_dull, heauy, benummed, clumsie, sluggish._"),
                    new("Vliuígn[o]", "of forme or colour of an oliue."),
                    new("Xisti[ó]ne", "_a kind of precious stone._"),
                    new("Xist[ó]ne", "_a place of exercise in faire weather, a wrestling-place._")
                }
            }
        };
    }
}