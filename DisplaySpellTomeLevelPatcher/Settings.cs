using Mutagen.Bethesda.Synthesis.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisplaySpellTomeLevelPatcher
{
    public class Settings
    {
        [SynthesisTooltip(@"Choisissez votre propre format ici ! Les variables disponibles sont : <level> (ex. Adepte), <spell> (ex. Clairvoyance), <plugin> (ex. Skyrim), <school> (ex. Altération). Le format par défaut est : Livre de sort (<level>) - <spell>")]
        public string Format { get; set; } = "Livre de sort (<level>) - <spell>";
    }
}
