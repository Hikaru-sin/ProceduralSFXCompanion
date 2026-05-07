using System.Collections.ObjectModel;
using ProceduralSFXCompanion.Models;

namespace ProceduralSFXCompanion.Models;

public class TutorialCardsModel
{
    public string? GroupName { get; set; }
    public ObservableCollection<WebTutorial>? Items { get; set; }
}