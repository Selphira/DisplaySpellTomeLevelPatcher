using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Immutable;

namespace DisplaySpellTomeLevelPatcher
{
    public class Program
    {
        private static Lazy<Settings> _settings = null!;
        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetAutogeneratedSettings(nickname: "Settings", path: "settings.json", out _settings)
                .SetTypicalOpen(GameRelease.SkyrimSE, "DisplaySpellTomeLevelPatcher.esp")
                .Run(args);
        }

        public static ModKey Vokrii = ModKey.FromNameAndExtension("Vokrii - Minimalistic Perks of Skyrim.esp");

        public static readonly HashSet<string> skillLevels = new HashSet<string>() {
            "Novice",
            "Apprenti",
            "Adept",
            "Expert",
            "Mastery"
        };

        public static readonly HashSet<string> magicSchools = new HashSet<string>()
        {
            "Guérison",
            "Destruction",
            "Conjuration",
            "Illusion",
            "Altération"
        };

        public const string levelFormatVariable = "<level>";
        public const string spellFormatVariable = "<spell>";
        public const string pluginFormatVariable = "<plugin>";
        public const string schoolFormatVariable = "<school>";

        public static Dictionary<string, string> spellLevelDictionary = new Dictionary<string, string>();

        public static string GenerateScrollName(string scrollName, string level)
        {
            return scrollName.Replace("Parchemin -", $"Parchemin ({level}) -");
        }

        public static string GetSpellNameFromSpellTome(string spellTomeName)
        {
            try
            {
                return spellTomeName.Split(" - ")[1];
            }
            catch (IndexOutOfRangeException)
            {
                return "";
            }
        }

        public static string GetSpellNameFromScroll(string scrollName)
        {
            string[] splitScrollName = scrollName.Split(" - ");
            string scrollSpellName = string.Join(" - ", splitScrollName.Skip(2).ToArray());
            return scrollSpellName;
        }
        public static bool NamedFieldsContain<TMajor>(TMajor named, string str)
            where TMajor : INamedGetter, IMajorRecordCommonGetter
        {
            if (named.EditorID?.Contains(str) ?? false) return true;
            if (named.Name?.Contains(str) ?? false) return true;
            return false;
        }

        public static bool DescriptionContain(IPerkGetter perkGetter, string str)
        {
            return perkGetter.Description?.String?.Contains(str) ?? false;
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            foreach (var bookContext in state.LoadOrder.PriorityOrder.Book().WinningContextOverrides())
            {
                IBookGetter book = bookContext.Record;

                if (book.Name?.String == null) continue;
                if (!book.Keywords?.Contains(Skyrim.Keyword.VendorItemSpellTome) ?? true) continue;
                if (book.Teaches is not IBookSpellGetter teachedSpell) continue;
                if (!teachedSpell.Spell.TryResolve(state.LinkCache, out var spell)) continue;
                if (!state.LinkCache.TryResolveContext(spell.HalfCostPerk.FormKey, spell.HalfCostPerk.Type, out var halfCostPerkContext)) continue;
                var halfCostPerk = (IPerkGetter)halfCostPerkContext.Record;
                if (halfCostPerk == null) continue;

                string spellName = GetSpellNameFromSpellTome(book.Name.String);
                if (spellName == "")
                {
                    Console.WriteLine($"{book.FormKey}: Could not get spell name from: {book.Name.String}");
                    continue;
                }

                string bookName = _settings.Value.Format;
                bool changed = false;
                if (bookName.Contains(levelFormatVariable))
                {
                    foreach (string skillLevel in skillLevels)
                    {
                        if (halfCostPerkContext.ModKey == Vokrii && halfCostPerk.Description != null)
                        {
                            if (!DescriptionContain(halfCostPerk, skillLevel)) continue;
                        }
                        else if (!NamedFieldsContain(halfCostPerk, skillLevel)) continue;

                        bookName = bookName.Replace(levelFormatVariable, skillLevel);
                        changed = true;
                        break;
                    }
                }
                if (halfCostPerkContext.ModKey == Vokrii && bookName.Contains(levelFormatVariable))
                {
                    bookName.Replace(levelFormatVariable, "Novice");
                }
                if (bookName.Contains(pluginFormatVariable))
                {
                    bookName = bookName.Replace(pluginFormatVariable, book.FormKey.ModKey.Name.ToString());
                    changed = true;
                }
                if (bookName.Contains(schoolFormatVariable))
                {
                    foreach (string spellSchool in magicSchools)
                    {
                        if (NamedFieldsContain(halfCostPerk, spellSchool) || DescriptionContain(halfCostPerk, spellSchool))
                        {
                            bookName = bookName.Replace(schoolFormatVariable, spellSchool);
                            changed = true;
                            break;
                        }
                    }
                }
                if (bookName.Contains(spellFormatVariable))
                {
                    bookName = bookName.Replace(spellFormatVariable, GetSpellNameFromSpellTome(book.Name.String));
                    changed = true;
                }
                if (changed && book.Name.String != bookName)
                {
                    Book bookToAdd = book.DeepCopy();
                    bookToAdd.Name = bookName;
                    state.PatchMod.Books.Set(bookToAdd);
                }
            }

            /*
            foreach (var scroll in state.LoadOrder.PriorityOrder.Scroll().WinningOverrides())
            {
                if (scroll.Name?.String == null) continue;

                string scrollSpellName = GetSpellNameFromScroll(scroll.Name.String);
                if (spellLevelDictionary.TryGetValue(scrollSpellName, out var skillLevel))
                {
                    Scroll scrollToAdd = scroll.DeepCopy();
                    scrollToAdd.Name = GenerateScrollName(scroll.Name.String, skillLevel);
                    state.PatchMod.Scrolls.Set(scrollToAdd);
                }
            }
            */
        }

    }
}
