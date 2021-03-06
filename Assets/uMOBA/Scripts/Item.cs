// The Item struct only contains the dynamic item properties and a name, so that
// the static properties can be read from the scriptable object.
//
// Items have to be structs in order to work with SyncLists.
//
// The player inventory actually needs Item slots that can sometimes be empty
// and sometimes contain an Item. The obvious way to do this would be a
// InventorySlot class that can store an Item, but SyncLists only work with
// structs - so the Item struct needs an option to be _empty_ to act like a
// slot. The simple solution to it is the _valid_ property in the Item struct.
// If valid is false then this Item is to be considered empty.
//
// _Note: the alternative is to have a list of Slots that can contain Items and
// to serialize them manually in OnSerialize and OnDeserialize, but that would
// be a whole lot of work and the workaround with the valid property is much
// simpler._
//
// Items can be compared with their name property, two items are the same type
// if their names are equal.
using System.Text;
using UnityEngine;
using Mirror;

[System.Serializable]
public struct Item {
    // name used to reference the database entry (cant save template directly
    // because synclist only support simple types)
    public string name;

    // dynamic stats (cooldowns etc. later)
    public bool valid; // acts as slot. false means there is no item in here.
    public int amount;

    // constructors
    public Item(ItemTemplate template, int _amount=1) {
        name = template.name;
        amount = _amount;
        valid = true;
    }

    // does the template still exist?
    public bool TemplateExists() {
        return name != null && ItemTemplate.dict.ContainsKey(name);
    }

    // database item property access
    public ItemTemplate template {
        get { return ItemTemplate.dict[name]; }
    }
    public string category {
        get { return template.category; }
    }
    public int maxStack {
        get { return template.maxStack; }
    }
    public int buyPrice {
        get { return template.buyPrice; }
    }
    public int sellPrice {
        get { return template.sellPrice; }
    }
    public Sprite image {
        get { return template.image; }
    }
    public bool usageDestroy {
        get { return template.usageDestroy; }
    }
    public int usageHealth {
        get { return template.usageHealth; }
    }
    public int usageMana {
        get { return template.usageMana; }
    }
    public int equipHealthBonus {
        get { return template.equipHealthBonus; }
    }
    public int equipManaBonus {
        get { return template.equipManaBonus; }
    }
    public int equipDamageBonus {
        get { return template.equipDamageBonus; }
    }
    public int equipDefenseBonus {
        get { return template.equipDefenseBonus; }
    }
    public float equipBlockChanceBonus {
        get { return template.equipBlockChanceBonus; }
    }
    public float equipCriticalChanceBonus {
        get { return template.equipCriticalChanceBonus; }
    }

    // fill in all variables into the tooltip
    // this saves us lots of ugly string concatenation code. we can't do it in
    // ItemTemplate because some variables can only be replaced here, hence we
    // would end up with some variables not replaced in the string when calling
    // Tooltip() from the template.
    // -> note: each tooltip can have any variables, or none if needed
    // -> example usage:
    /*
    <b>{NAME}</b>
    Description here...

    {EQUIPDAMAGEBONUS} Damage
    {EQUIPDEFENSEBONUS} Defense
    {EQUIPHEALTHBONUS} Health
    {EQUIPMANABONUS} Mana
    {EQUIPBLOCKCHANCEBONUS} Block
    {EQUIPCRITICALCHANCEBONUS} Critical
    Restores {USAGEHEALTH} Health on use.
    Restores {USAGEMANA} Mana on use.

    Amount: {AMOUNT}
    Price: {BUYPRICE} Gold
    <i>Sells for: {SELLPRICE} Gold</i>
    */
    public string ToolTip() {
        // we use a StringBuilder so that addons can modify tooltips later too
        // ('string' itself can't be passed as a mutable object)
        var tip = new StringBuilder(template.toolTip);
        tip.Replace("{NAME}", name);
        tip.Replace("{CATEGORY}", category);
        tip.Replace("{EQUIPDAMAGEBONUS}", equipDamageBonus.ToString());
        tip.Replace("{EQUIPDEFENSEBONUS}", equipDefenseBonus.ToString());
        tip.Replace("{EQUIPHEALTHBONUS}", equipHealthBonus.ToString());
        tip.Replace("{EQUIPMANABONUS}", equipManaBonus.ToString());
        tip.Replace("{EQUIPBLOCKCHANCEBONUS}", Mathf.RoundToInt(equipBlockChanceBonus * 100).ToString());
        tip.Replace("{EQUIPCRITICALCHANCEBONUS}", Mathf.RoundToInt(equipCriticalChanceBonus * 100).ToString());
        tip.Replace("{USAGEHEALTH}", usageHealth.ToString());
        tip.Replace("{USAGEMANA}", usageMana.ToString());
        tip.Replace("{BUYPRICE}", buyPrice.ToString());
        tip.Replace("{SELLPRICE}", sellPrice.ToString());
        tip.Replace("{AMOUNT}", amount.ToString());
        return tip.ToString();
    }
}

public class SyncListItem : SyncList<Item> { }
