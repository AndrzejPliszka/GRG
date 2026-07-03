using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIReferences : MonoBehaviour
{
    public TMP_Text lookedAtObjectText;
    public TMP_Text hungerBarText;
    public TMP_Text healthBarText;

    public TMP_Text moneyCount;
    public TMP_Text taxRateText;
    public TMP_Text criminalText;
    public TMP_Text tooltipsText;

    public TMP_Text woodMaterialText;
    public TMP_Text foodMaterialText;
    public TMP_Text stoneMaterialText;

    public Image hitmark;
    public Image cooldownMarker;
    public Image micActivityIcon;

    public Slider hungerBar;
    public Slider healthBar;

    public GameObject inventorySlotsContainer;
    public List<GameObject> inventorySlots;

    public Image mainSlotBuilding;
    public Image previousSlotBuilding;
    public Image nextSlotBuilding;

    public TMP_Text selectedBuildingText;
    public Transform buildMenu;

    public Slider activityProgressBar;
}
