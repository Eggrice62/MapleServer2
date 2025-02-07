﻿using MapleServer2.Database.Types;
using SqlKata.Execution;

namespace MapleServer2.Database.Classes;

public class DatabaseShop : DatabaseTable
{
    public DatabaseShop() : base("shops") { }

    public Shop FindById(int id)
    {
        dynamic data = QueryFactory.Query(TableName).Where("id", id).Get().FirstOrDefault();
        if (data == null)
        {
            return null;
        }
        Shop shop = ReadShop(data);
        shop.Items = DatabaseManager.ShopItems.FindAllByShopId(shop.Id);
        return shop;
    }

    private static Shop ReadShop(dynamic data)
    {
        return new Shop(data.id, data.category, data.name, data.shop_type, data.restrict_sales, data.can_restock, data.next_restock, data.allow_buyback);
    }
}
