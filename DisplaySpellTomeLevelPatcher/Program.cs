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

        public static HashSet<string> skillLevels = new HashSet<string>() {
            "Novice",
            "Apprentice",
            "Adept",
            "Expert",
            "Master"
        };
       
        public static Dictionary<string, string> spellLevelDictionary = new Dictionary<string, string>();

        public static string GenerateSpellTomeName(string spellTomeName, string level)
        {
            return spellTomeName.Replace("Spell Tome:", $"Spell Tome ({level}):");
        }

        public static string GenerateAlternativeSpellTomeName(string spellTomeName, string level)
        {
            return spellTomeName.Replace("Spell Tome:", $"{level} Spell Tome:");
        }

        public static string GenerateScrollName(string scrollName, string level)
        {
            return scrollName.Replace("Scroll of", $"Scroll ({level}):");
        }

        public static string GetSpellNameFromSpellTome(string spellTomeName)
        {
            try
            {
                return spellTomeName.Split(": ")[1];
            }
            catch (IndexOutOfRangeException)
            {
                return "";
            }
        }

        public static string GetSpellNameFromScroll(string scrollName)
        {
            string[] splitScrollName = scrollName.Split(' ');
            string scrollSpellName = string.Join(' ', splitScrollName.Skip(2).ToArray());
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
                if (book.Teaches is not IBookSpellGetter spellTeach) continue;
                if (!spellTeach.Spell.TryResolve(state.LinkCache, out var spell)) continue;
                if (!state.LinkCache.TryResolveContext(spell.HalfCostPerk.FormKey, spell.HalfCostPerk.Type, out var halfCostPerkContext)) continue;
                var halfCostPerk = (IPerkGetter)halfCostPerkContext.Record;
                if (halfCostPerk == null) continue;

                string spellName = GetSpellNameFromSpellTome(book.Name.String);
                if (spellName == "")
                {
                    Console.WriteLine($"{book.FormKey}: Could not get spell name from: {book.Name.String}");
                    continue;
                }

                foreach (string skillLevel in skillLevels)
                {
                    if (halfCostPerkContext.ModKey == Vokrii && halfCostPerk.Description != null)
                    {
                        if (!DescriptionContain(halfCostPerk, skillLevel)) continue;
                    }
                    else if (!NamedFieldsContain(halfCostPerk, skillLevel)) continue;

                    string generatedName = _settings.Value.AlternativeNaming ? GenerateAlternativeSpellTomeName(book.Name.String, skillLevel) : GenerateSpellTomeName(book.Name.String, skillLevel);
                    if (generatedName == book.Name.String) continue;

                    //spellLevelDictionary[spellName] = skillLevel;
                    Book bookToAdd = book.DeepCopy();
                    bookToAdd.Name = generatedName;
                    state.PatchMod.Books.Set(bookToAdd);
                    break;
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
