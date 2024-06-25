using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ArabicStemmer
{
    /// <summary>
    /// The Stemmer class provides methods for stemming Arabic words.
    /// </summary>
    public class Stemmer
    {
        private static List<HashSet<string>> StaticFiles;
        public static string PathToStemmerFiles = "StemmerFiles/";

        private static bool RootFound;
        private static bool StopwordFound;
        private static bool FromSuffixes;
        private static string[][] StemmedDocument = new string[10000][];
        private static int WordNumber;
        private static List<string> ListStemmedWords;
        private static List<string> ListRootsFound;
        private static List<string> ListStopwordsFound;
        private static List<string> ListOriginalStopword;
        private static string[][] PossibleRoots = new string[10000][];
        private static Dictionary<string, string[]> Cache = new Dictionary<string, string[]>();

        /// <summary>
        /// Initializes a new instance of the Stemmer class.
        /// </summary>
        /// <param name="stemFilesPath">The path to the stemmer files.</param>
        public Stemmer(string stemFilesPath)
        {
            PathToStemmerFiles = stemFilesPath;
            InitComponents();
            LoadStemmerFiles(stemFilesPath);
            InitVectorFiles();
        }

        /// <summary>
        /// Initializes components and resets state.
        /// </summary>
        private static void InitComponents()
        {
            RootFound = false;
            StopwordFound = false;
            FromSuffixes = false;
            StemmedDocument = new string[10000][];
            WordNumber = 0;
            ListStemmedWords = new List<string>();
            ListRootsFound = new List<string>();
            ListStopwordsFound = new List<string>();
            ListOriginalStopword = new List<string>();

            for (int i = 0; i < 10000; i++)
            {
                StemmedDocument[i] = new string[3];
                PossibleRoots[i] = new string[100];
            }
        }

        /// <summary>
        /// Loads stemmer files into memory.
        /// </summary>
        /// <param name="path">The path to the stemmer files.</param>
        private static void LoadStemmerFiles(string path)
        {
            var fileNames = new[]
            {
                "definite_article.txt", "diacritics.txt", "duplicate.txt", "first_waw.txt",
                "first_yah.txt", "last_alif.txt", "last_hamza.txt", "last_maksoura.txt",
                "last_yah.txt", "mid_waw.txt", "mid_yah.txt", "prefixes.txt",
                "punctuation.txt", "quad_roots.txt", "stopwords.txt", "strange.txt",
                "suffixes.txt", "tri_patt.txt", "tri_roots.txt"
            };

            foreach (var fileName in fileNames)
            {
                Cache[fileName] = File.ReadAllLines(Path.Combine(path, fileName));
            }
        }

        /// <summary>
        /// Initializes vector files from cached data.
        /// </summary>
        private static void InitVectorFiles()
        {
            StaticFiles = new List<HashSet<string>>();
            string[] fileNames = {
                "definite_article.txt", "duplicate.txt", "first_waw.txt", "first_yah.txt",
                "last_alif.txt", "last_hamza.txt", "last_maksoura.txt", "last_yah.txt",
                "mid_waw.txt", "mid_yah.txt", "prefixes.txt", "punctuation.txt",
                "quad_roots.txt", "stopwords.txt", "suffixes.txt", "tri_patt.txt",
                "tri_roots.txt", "diacritics.txt", "strange.txt"
            };

            foreach (var fileName in fileNames)
            {
                AddVectorFromFile(fileName);
            }
        }

        /// <summary>
        /// Adds vector data from a file to the static files list.
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        private static void AddVectorFromFile(string fileName)
        {
            var vectorFromFile = new HashSet<string>(Cache[fileName].SelectMany(line => line.Split(" ", StringSplitOptions.RemoveEmptyEntries)));
            StaticFiles.Add(vectorFromFile);
        }

        /// <summary>
        /// Checks for prefixes in the given word.
        /// </summary>
        /// <param name="word">The word to check.</param>
        /// <returns>The modified word without the prefix.</returns>
        private static string CheckForPrefixes(string word)
        {
            Debug.Print("Enter CheckForPrefix " + word);
            string modifiedWord = word;
            var prefixes = StaticFiles[(int)LinguisticFiles.Prefixes];
            foreach (var prefix in prefixes)
            {
                if (modifiedWord.StartsWith(prefix))
                {
                    modifiedWord = modifiedWord.Substring(prefix.Length);

                    if (CheckStopwords(modifiedWord)) return modifiedWord;
                    modifiedWord = ProcessWordByLength(modifiedWord);

                    if (StopwordFound) return modifiedWord;
                    if (RootFound && !StopwordFound) return modifiedWord;
                }
            }
            return word;
        }

        /// <summary>
        /// Checks if the current word is a stopword.
        /// </summary>
        /// <param name="currentWord">The current word.</param>
        /// <returns>True if the word is a stopword, otherwise false.</returns>
        private static bool CheckStopwords(string currentWord)
        {
            var stopwords = StaticFiles[(int)LinguisticFiles.Stopwords];
            if (StopwordFound = stopwords.Contains(currentWord))
            {
                StemmedDocument[WordNumber][1] = currentWord;
                StemmedDocument[WordNumber][2] = "STOPWORD";
                ListStopwordsFound.Add(currentWord);
                ListOriginalStopword.Add(StemmedDocument[WordNumber][0]);
            }
            return StopwordFound;
        }

        /// <summary>
        /// Processes a word by its length.
        /// </summary>
        /// <param name="word">The word to process.</param>
        /// <returns>The modified word.</returns>
        private static string ProcessWordByLength(string word)
        {
            if (word.Length == 2) return IsTwoLetters(word);
            if (word.Length == 3 && !RootFound) return IsThreeLetters(word);
            if (word.Length == 4) IsFourLetters(word);
            if (!RootFound && word.Length > 2) word = CheckPatterns(word);
            if (!RootFound && !StopwordFound && !FromSuffixes) word = CheckForSuffixes(word);
            return word;
        }

        /// <summary>
        /// Processes a two-letter word.
        /// </summary>
        /// <param name="word">The word to process.</param>
        /// <returns>The modified word.</returns>
        private static string IsTwoLetters(string word)
        {
            word = HandleDuplicate(word);
            if (!RootFound) word = HandleLastWeak(word);
            if (!RootFound) word = HandleFirstWeak(word);
            if (!RootFound) word = HandleMiddleWeak(word);
            return word;
        }

        /// <summary>
        /// Processes a three-letter word.
        /// </summary>
        /// <param name="word">The word to process.</param>
        /// <returns>The modified word.</returns>
        private static string IsThreeLetters(string word)
        {
            var modifiedWord = new StringBuilder(word);
            string root = "";

            if (word.Length > 0)
            {
                if (word[0] == '\u0627' || word[0] == '\u0624' || word[0] == '\u0626')
                {
                    modifiedWord[0] = '\u0623';
                    root = modifiedWord.ToString();
                }

                if (word[2] == '\u0648' || word[2] == '\u064a' || word[2] == '\u0627' || word[2] == '\u0649' || word[2] == '\u0621' || word[2] == '\u0626')
                {
                    root = word.Substring(0, 2);
                    root = HandleLastWeak(root);
                    if (RootFound) return root;
                }

                if (word[1] == '\u0648' || word[1] == '\u064a' || word[1] == '\u0627' || word[1] == '\u0626')
                {
                    root = word[0] + word.Substring(2);
                    root = HandleMiddleWeak(root);
                    if (RootFound) return root;
                }

                if (word[1] == '\u0624' || word[1] == '\u0626')
                {
                    root = word[0] + (word[2] == '\u0645' || word[2] == '\u0632' || word[2] == '\u0631' ? "\u0627" : "\u0623") + word.Substring(2);
                }

                if (word[2] == '\u0651')
                {
                    root = word[0] + word.Substring(1, 2);
                }
            }

            if (root.Length == 0 && StaticFiles[(int)LinguisticFiles.TriRoots].Contains(word))
            {
                RootFound = true;
                StemmedDocument[WordNumber][1] = word;
                StemmedDocument[WordNumber][2] = "ROOT";
                ListStemmedWords.Add(StemmedDocument[WordNumber][0]);
                ListRootsFound.Add(word);
                return word;
            }
            else if (StaticFiles[(int)LinguisticFiles.TriRoots].Contains(root))
            {
                RootFound = true;
                StemmedDocument[WordNumber][1] = root;
                StemmedDocument[WordNumber][2] = "ROOT";
                ListStemmedWords.Add(StemmedDocument[WordNumber][0]);
                ListRootsFound.Add(word);
                return root;
            }

            return word;
        }

        /// <summary>
        /// Processes a four-letter word.
        /// </summary>
        /// <param name="word">The word to process.</param>
        private static void IsFourLetters(string word)
        {
            if (StaticFiles[(int)LinguisticFiles.QuadRoots].Contains(word))
            {
                RootFound = true;
                StemmedDocument[WordNumber][1] = word;
                StemmedDocument[WordNumber][2] = "ROOT";
                ListStemmedWords.Add(StemmedDocument[WordNumber][0]);
                ListRootsFound.Add(word);
            }
        }

        /// <summary>
        /// Checks if the word matches any known patterns.
        /// </summary>
        /// <param name="word">The word to check.</param>
        /// <returns>The modified word if a pattern is found.</returns>
        private static string CheckPatterns(string word)
        {
            var root = new StringBuilder();
            if (word.Length > 0 && (word[0] == '\u0623' || word[0] == '\u0625' || word[0] == '\u0622'))
            {
                root.Append('\u0627').Append(word.Substring(1));
                word = root.ToString();
            }

            var patterns = StaticFiles[(int)LinguisticFiles.TriPatt];
            int numberSameLetters = 0;
            string pattern = "";
            string modifiedWord = "";

            foreach (var item in patterns)
            {
                pattern = item;
                root.Clear();
                if (pattern.Length == word.Length)
                {
                    numberSameLetters = 0;

                    for (int j = 0; j < word.Length; j++)
                    {
                        if (pattern[j] == word[j] && pattern[j] != '\u0641' && pattern[j] != '\u0639' && pattern[j] != '\u0644')
                        {
                            numberSameLetters++;
                        }
                    }

                    if (word.Length == 6 && word[3] == word[5] && numberSameLetters == 2)
                    {
                        root.Append(word[1]).Append(word[2]).Append(word[3]);
                        modifiedWord = root.ToString();
                        modifiedWord = IsThreeLetters(modifiedWord);
                        if (RootFound) return modifiedWord;
                    }

                    if (word.Length - 3 <= numberSameLetters)
                    {
                        for (int j = 0; j < word.Length; j++)
                        {
                            if (pattern[j] == '\u0641' || pattern[j] == '\u0639' || pattern[j] == '\u0644')
                            {
                                root.Append(word[j]);
                            }
                        }

                        modifiedWord = root.ToString();
                        modifiedWord = IsThreeLetters(modifiedWord);

                        if (RootFound) return modifiedWord;
                    }
                }
            }
            return word;
        }

        /// <summary>
        /// Checks for suffixes in the given word.
        /// </summary>
        /// <param name="word">The word to check.</param>
        /// <returns>The modified word without the suffix.</returns>
        private static string CheckForSuffixes(string word)
        {
            var suffixes = StaticFiles[(int)LinguisticFiles.Suffixes];
            FromSuffixes = true;

            foreach (var suffix in suffixes)
            {
                if (word.EndsWith(suffix))
                {
                    var modifiedWord = word.Substring(0, word.Length - suffix.Length);
                    if (CheckStopwords(modifiedWord)) return ResetFromSuffixes(modifiedWord);
                    modifiedWord = ProcessWordByLength(modifiedWord);

                    if (StopwordFound) return ResetFromSuffixes(modifiedWord);
                    if (RootFound) return ResetFromSuffixes(modifiedWord);
                }
            }
            FromSuffixes = false;
            return word;
        }

        /// <summary>
        /// Resets the FromSuffixes flag and returns the modified word.
        /// </summary>
        /// <param name="modifiedWord">The modified word.</param>
        /// <returns>The modified word.</returns>
        private static string ResetFromSuffixes(string modifiedWord)
        {
            FromSuffixes = false;
            return modifiedWord;
        }

        /// <summary>
        /// Handles duplicate words.
        /// </summary>
        /// <param name="word">The word to handle.</param>
        /// <returns>The modified word.</returns>
        private static string HandleDuplicate(string word)
        {
            if (StaticFiles[(int)LinguisticFiles.Duplicate].Contains(word))
            {
                word += word.Substring(1);
                RootFound = true;
                StemmedDocument[WordNumber][1] = word;
                StemmedDocument[WordNumber][2] = "ROOT";
                ListStemmedWords.Add(StemmedDocument[WordNumber][0]);
                ListRootsFound.Add(word);
                return word;
            }
            return word;
        }

        /// <summary>
        /// Handles words with a weak last character.
        /// </summary>
        /// <param name="word">The word to handle.</param>
        /// <returns>The modified word.</returns>
        private static string HandleLastWeak(string word)
        {
            var stemmedWord = new StringBuilder();
            if (StaticFiles[(int)LinguisticFiles.LastAlif].Contains(word))
            {
                stemmedWord.Append(word).Append("\u0627");
                word = stemmedWord.ToString();
                stemmedWord.Clear();
                RootFound = true;
                StemmedDocument[WordNumber][1] = word;
                StemmedDocument[WordNumber][2] = "ROOT";
                ListStemmedWords.Add(StemmedDocument[WordNumber][0]);
                ListRootsFound.Add(word);
                return word;
            }
            else if (StaticFiles[(int)LinguisticFiles.LastHamza].Contains(word))
            {
                stemmedWord.Append(word).Append("\u0623");
                word = stemmedWord.ToString();
                stemmedWord.Clear();
                RootFound = true;
                StemmedDocument[WordNumber][1] = word;
                StemmedDocument[WordNumber][2] = "ROOT";
                ListStemmedWords.Add(StemmedDocument[WordNumber][0]);
                ListRootsFound.Add(word);
                return word;
            }
            else if (StaticFiles[(int)LinguisticFiles.LastMaksoura].Contains(word))
            {
                stemmedWord.Append(word).Append("\u0649");
                word = stemmedWord.ToString();
                stemmedWord.Clear();
                RootFound = true;
                StemmedDocument[WordNumber][1] = word;
                StemmedDocument[WordNumber][2] = "ROOT";
                ListStemmedWords.Add(StemmedDocument[WordNumber][0]);
                ListRootsFound.Add(word);
                return word;
            }
            else if (StaticFiles[(int)LinguisticFiles.LastYah].Contains(word))
            {
                stemmedWord.Append(word).Append("\u064a");
                word = stemmedWord.ToString();
                stemmedWord.Clear();
                RootFound = true;
                StemmedDocument[WordNumber][1] = word;
                StemmedDocument[WordNumber][2] = "ROOT";
                ListStemmedWords.Add(StemmedDocument[WordNumber][0]);
                ListRootsFound.Add(word);
                return word;
            }
            return word;
        }

        /// <summary>
        /// Handles words with a weak first character.
        /// </summary>
        /// <param name="word">The word to handle.</param>
        /// <returns>The modified word.</returns>
        private static string HandleFirstWeak(string word)
        {
            var stemmedWord = new StringBuilder();
            if (StaticFiles[(int)LinguisticFiles.FirstWaw].Contains(word))
            {
                stemmedWord.Append("\u0648").Append(word);
                word = stemmedWord.ToString();
                stemmedWord.Clear();
                RootFound = true;
                StemmedDocument[WordNumber][1] = word;
                StemmedDocument[WordNumber][2] = "ROOT";
                ListStemmedWords.Add(StemmedDocument[WordNumber][0]);
                ListRootsFound.Add(word);
                return word;
            }
            else if (StaticFiles[(int)LinguisticFiles.FirstYah].Contains(word))
            {
                stemmedWord.Append("\u064a").Append(word);
                word = stemmedWord.ToString();
                stemmedWord.Clear();
                RootFound = true;
                StemmedDocument[WordNumber][1] = word;
                StemmedDocument[WordNumber][2] = "ROOT";
                ListStemmedWords.Add(StemmedDocument[WordNumber][0]);
                ListRootsFound.Add(word);
                return word;
            }
            return word;
        }

        /// <summary>
        /// Handles words with a weak middle character.
        /// </summary>
        /// <param name="word">The word to handle.</param>
        /// <returns>The modified word.</returns>
        private static string HandleMiddleWeak(string word)
        {
            var stemmedWord = new StringBuilder("j");
            if (StaticFiles[(int)LinguisticFiles.MidWaw].Contains(word))
            {
                stemmedWord.Append(word[0]).Append("\u0648").Append(word.Substring(1));
                word = stemmedWord.ToString();
                stemmedWord.Clear();
                RootFound = true;
                StemmedDocument[WordNumber][1] = word;
                StemmedDocument[WordNumber][2] = "ROOT";
                ListStemmedWords.Add(StemmedDocument[WordNumber][0]);
                ListRootsFound.Add(word);
                return word;
            }
            else if (StaticFiles[(int)LinguisticFiles.MidYah].Contains(word))
            {
                stemmedWord.Append(word[0]).Append("\u064a").Append(word.Substring(1));
                word = stemmedWord.ToString();
                stemmedWord.Clear();
                RootFound = true;
                StemmedDocument[WordNumber][1] = word;
                StemmedDocument[WordNumber][2] = "ROOT";
                ListStemmedWords.Add(StemmedDocument[WordNumber][0]);
                ListRootsFound.Add(word);
                return word;
            }
            return word;
        }

        /// <summary>
        /// Stems the given word.
        /// </summary>
        /// <param name="word">The word to stem.</param>
        /// <returns>The stemmed word.</returns>
        public string StemWord(string word)
        {
            InitComponents();

            word = ProcessWordByLength(word);
            if (!RootFound) word = CheckDefiniteArticle(word);
            if (!RootFound && !StopwordFound) word = CheckPrefixWaw(word);
            if (!RootFound && !StopwordFound) word = CheckForSuffixes(word);
            if (!RootFound && !StopwordFound) word = CheckForPrefixes(word);
            return word.Replace("j","");
        }

        /// <summary>
        /// Checks for definite articles in the given word.
        /// </summary>
        /// <param name="word">The word to check.</param>
        /// <returns>The modified word without the definite article.</returns>
        private static string CheckDefiniteArticle(string word)
        {
            var definiteArticles = StaticFiles[(int)LinguisticFiles.DefiniteArticle];
            string modifiedWord = word;
            foreach (var article in definiteArticles)
            {
                if (word.StartsWith(article))
                {
                    modifiedWord = word.Substring(article.Length);
                    if (CheckStopwords(modifiedWord)) return modifiedWord;
                    modifiedWord = ProcessWordByLength(modifiedWord);

                    if (StopwordFound) return modifiedWord;
                    if (RootFound && !StopwordFound) return modifiedWord;
                }
            }
            return modifiedWord.Length > 3 ? modifiedWord : word;
        }

        /// <summary>
        /// Checks for the prefix "Waw" in the given word.
        /// </summary>
        /// <param name="word">The word to check.</param>
        /// <returns>The modified word without the "Waw" prefix.</returns>
        private static string CheckPrefixWaw(string word)
        {
            if (word.Length > 3 && word[0] == '\u0648')
            {
                var modifiedWord = word.Substring(1);
                if (CheckStopwords(modifiedWord)) return modifiedWord;
                modifiedWord = ProcessWordByLength(modifiedWord);

                if (StopwordFound) return modifiedWord;
                if (RootFound && !StopwordFound) return modifiedWord;
            }
            return word;
        }

        /// <summary>
        /// Returns the possible roots found during stemming.
        /// </summary>
        /// <returns>An array of possible roots.</returns>
        public string[][] ReturnPossibleRoots()
        {
            return PossibleRoots;
        }
    }
}
