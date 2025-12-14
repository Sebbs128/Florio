using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using Florio.Parsers.Gutenberg;

namespace Florio.Utilities.BenchmarkTrials.Parsing;

// for comparing between changes to GutenbergTextParser
[MemoryDiagnoser]
public class ParseLinesBenchmark
{
    private static readonly string _contentToParse = """
                            
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

            Ẻssere, s[o]n[o], fui, f[ó]ra, stát[o] _or_ sút[o], _to be._

            Fáre a guísa délla c[ó]da del pórc[o] che tútt[o] il gi[ó]rn[o] se la
            diména, e pói la séra n[o]n hà fátt[o] núlla, _to doe as the hog doth
            that all day wags his taile and at night hath done nothing, much adoe
            and neuer the neerer, doe and vndoe the day is long enough._

            Fattaménte, si, _being ioined thereunto, in such sort, guise, fashion
            or manner._

            Fátt[o] _or_ fátta, _following_ Sì, _or_ C[o]sì, _serueth for such, so
            made, or of such quality._

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

            [Ó]rl[o], _a hem, a welt, a brim, a ledge, a seluedge, an edge, a
            border, of any thing. Also an orle in armory, the placing of any thing
            in, about or vpon a border._

              [Ó]rl[o] álla spagnuóla.  }
                                        }
              [Ó]rl[o] crésp[o].        }
                                        }  _Certaine hemes
              [Ó]rl[o] pertugiát[o].    }   so called of
                                        }   Seamsters._
              [Ó]rl[o] pián[o].         }
                                        }
              [Ó]rl[o] retín[o].        }

            Prẻzzáre,_ as_ Prẻgiáre, _to bargane or make price for any thing._

            Pr<i>ò</i> Pr<i>ò</i>, _much much good may it doe you, well may you fare._

            Rint[er]rzáta cárta, _a bun-carde. Also a carde prickt or packt for aduantage._

            R[o]mpicóll[o],_ a breake-necke place, a downefal, a headlong
            precipice,_ A r[o] mpicóll[o], _headlong, rashly, desperately, in danger
            of breaking ones necke.Also a desperate, rash or heedlesse fellow._

            Sẻttezz[ó]ni, p[ó]nti, c[o]lisẻi, acqued[ó]tti, & sẻttezz[ó]ni, _a kind
            of proud fabrike._

            S[o]praẻssere, s[o]pras[ó]n[o], fúi, stát[o], _to be ouer or vpon.
            Also to be superfluous or more then enough. Also to remaine and suruiue
            others._

            S[o]prandáre, uád[o], andái, andát[o], _to ouergoe, to out-goe, to goe
            ouer or beyond._

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
            """;
    private InMemoryDownloader? _downloader;
    private GutenbergTextParser? _parser;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _downloader = new InMemoryDownloader(_contentToParse);
        _parser = new GutenbergTextParser(_downloader);
    }

    [Benchmark]
    public async Task<int> Parse()
    {
        var list = await _parser!.ParseLines(CancellationToken.None).ToListAsync();
        return list.Count;
    }

    class InMemoryDownloader(string input) : IGutenbergTextDownloader
    {
        private readonly string _input = input;

        public async IAsyncEnumerable<string> ReadLines([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(_input));
            var reader = new StreamReader(stream);
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null && !cancellationToken.IsCancellationRequested)
            {
                yield return line;
            }
        }
    }
}
