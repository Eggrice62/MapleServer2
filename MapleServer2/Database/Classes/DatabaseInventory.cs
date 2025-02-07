﻿using Maple2Storage.Enums;
using MapleServer2.Types;
using Newtonsoft.Json;
using SqlKata.Execution;

namespace MapleServer2.Database.Classes;

public class DatabaseInventory : DatabaseTable
{
    public DatabaseInventory() : base("inventories") { }

    public long Insert(IInventory inventory)
    {
        return QueryFactory.Query(TableName).InsertGetId<long>(new
        {
            extra_size = JsonConvert.SerializeObject(inventory.ExtraSize)
        });
    }

    public IInventory FindById(long id)
    {
        return ReadInventory(QueryFactory.Query(TableName).Where("id", id).FirstOrDefault());
    }

    public void Update(IInventory inventory)
    {
        QueryFactory.Query(TableName).Where("id", inventory.Id).Update(new
        {
            extra_size = JsonConvert.SerializeObject(inventory.ExtraSize)
        });

        List<Item> items = new();
        items.AddRange(inventory.GetItemsNotNull().ToList());
        items.AddRange(inventory.Equips.Values.Where(x => x != null).ToList());
        items.AddRange(inventory.Badges.Where(x => x != null).ToList());
        items.AddRange(inventory.Cosmetics.Values.Where(x => x != null).ToList());
        items.AddRange(inventory.LapenshardStorage.Where(x => x != null).ToList());

        foreach (Item item in items)
        {
            item.InventoryId = inventory.Id;
            item.BankInventoryId = 0;
            item.HomeId = 0;
            DatabaseManager.Items.Update(item);
        }
    }

    public bool Delete(long id)
    {
        return QueryFactory.Query(TableName).Where("id", id).Delete() == 1;
    }

    private static IInventory ReadInventory(dynamic data)
    {
        return new Inventory(data.id, JsonConvert.DeserializeObject<Dictionary<InventoryTab, short>>(data.extra_size), DatabaseManager.Items.FindAllByInventoryId(data.id));
    }
}
