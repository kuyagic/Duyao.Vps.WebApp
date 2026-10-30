using LinqToDB.CodeModel;
using LinqToDB.DataModel;
using LinqToDB.Schema;

namespace ScApi.Linq2Db.Tool;

public class CustomInterceptor : LinqToDB.Scaffold.ScaffoldInterceptors
{
    /// <summary>
    /// 测试 Better Comment
    /// *
    /// !
    /// ~ 
    /// > 
    /// TODO
    /// </summary>
    /// <param name="databaseType"></param>
    /// <param name="typeParser"></param>
    /// <param name="defaultMapping"></param>
    /// <returns></returns>
    public override TypeMapping? GetTypeMapping(DatabaseType databaseType, ITypeParser typeParser,
        TypeMapping? defaultMapping)
    {
        if (databaseType.Name?.ToLower().StartsWith("tinyint"
                , StringComparison.CurrentCultureIgnoreCase) ?? false)
        {
            return new TypeMapping(typeParser.Parse<int>(), null);
        }

        return defaultMapping;
    }

    public override IEnumerable<Table> GetTables(IEnumerable<Table> tables)
    {
        var t = tables.ToList();
        var filteredTables = t.Where(table =>
        {
            var name = table.Name.Name; // 或者 table.Name，取决于具体版本属性名，通常是 TableName

            if (string.IsNullOrEmpty(name)) return true;

            var lst = new[]
            {
                "basis_account_car",
                "basis_account_servicer",
                "basis_app_secret_config",
                "basis_bd_captain_relation",
                "basis_bd_daily_route_task_schedule",
                "basis_bd_user_commission_history",
                "basis_bd_voucher_history",
                "basis_brand_street_relation",
                "basis_brand_supplier_shop_relation",
                "basis_captain_group_fee_rule",
                "basis_captain_order_fee_detail",
                "basis_captain_street_relation",
                "basis_category_channel_relation",
                "basis_category_street_relation",
                "basis_comment_config",
                "basis_currency_coin_history",
                "basis_currency_coin_info",
                "basis_currency_withdraw_request_info",
                "basis_customer_area",
                "basis_deposit_supplier_info",
                "basis_deposit_supplier_operation_log",
                "basis_expiry_product_shelf",
                "basis_iap_config",
                "basis_iap_order",
                "basis_iap_pay_transaction_relation",
                "basis_order_after_sale_refund_quota_history",
                "basis_order_comment",
                "basis_order_expiry_detail",
                "basis_order_redeploy_detail",
                "basis_platform_user_ui_permission",
                "basis_product_info_feedback",
                "basis_product_price_feedback",
                "basis_product_profit_event",
                "basis_product_redeem_config",
                "basis_product_suggest_retail_price",
                "basis_role_info",
                "basis_shop_address_extinfo",
                "basis_shop_bd_relation_remove_20230911",
                "basis_shop_delivery_order_detail",
                "basis_shop_delivery_order_info",
                "basis_shop_platform_user_relation",
                "basis_shop_point_history",
                "basis_shop_pre_product_item",
                "basis_shop_product_commission_info",
                "basis_shop_product_display_info",
                "basis_shop_push_config",
                "basis_shop_search_history",
                "basis_shop_shelf_ext_info",
                "basis_shop_timeline_history",
                "basis_street_customer_area_relation",
                "basis_supplier_brand_relation",
                "basis_supplier_category_relationbak",
                "basis_supplier_config",
                "basis_supplier_driver_relation",
                "basis_supplier_event_info",
                "basis_supplier_refund_shelf",
                "basis_supplier_shelf_check_history",
                "basis_supplier_shelf_check_progress",
                "basis_supplier_shop_relation",
                "basis_supplier_street_relation",
                "basis_system_log",
                "basis_user_favourite_product",
                "basis_user_voucher",
                "basis_voucher_config",
                "basis_warehouse_item_stock",
                "basis_warehouse_item_stock_change_log",
                "basis_warehouse_location",
                "basis_warehouse_order_summary_detail",
                "basis_warehouse_order_summary_info",
                "basis_warehouse_order_summary_relation",
                "basis_warehouse_pay_transaction_relation",
                "core_warehouse_shelf_relation",
                "order_supplier_shop_relation",
                "shop_brand_currency_coin_history",
                "shop_brand_currency_coin_info",
                "shop_voucher_currency_coin_history",
                "warehouse_liquidation_order_detail",
                "warehouse_liquidation_order_info",
                "warehouse_refund_record_detail",
                "warehouse_refund_record_info",
                "warehouse_stock_adjust_detail",
                "warehouse_stock_adjust_info",
                "warehouse_stock_check_detail",
                "warehouse_stock_check_info",
                "wx_corp_external_contact_info",
                "category_channel_relation",

                "dbms_supplier_chains_product.basis_product_fee_rule"
            };

            if (lst.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            // 同样的过滤逻辑
            if (name.Contains("_bak", System.StringComparison.OrdinalIgnoreCase)) return false;
            if (name.Contains(".bak", System.StringComparison.OrdinalIgnoreCase)) return false;

            if (name.StartsWith("cash", System.StringComparison.OrdinalIgnoreCase)) return false;
            if (name.StartsWith("delivery_", System.StringComparison.OrdinalIgnoreCase)) return false;
            if (name.StartsWith("dw", System.StringComparison.OrdinalIgnoreCase)) return false; // 这里过滤掉 DW_xxx
            if (name.StartsWith("wm", System.StringComparison.OrdinalIgnoreCase)) return false;
            if (name.StartsWith("tmp_", System.StringComparison.OrdinalIgnoreCase)) return false;

            if (name.StartsWith("hosting", System.StringComparison.OrdinalIgnoreCase)) return false;

            //虚拟列
            // basis_order_event_result_2026.limit_item_code
            
            return true;
        });

        var enumerable = filteredTables.ToList();
        Console.WriteLine($"[FILTER] Table count: {t.Count}, Filtered count: {enumerable.Count}");

        return enumerable;
    }

    public override void PreprocessEntity(ITypeParser typeParser, EntityModel entityModel)
    {
        var dict = new Dictionary<string, List<string>>
        {
            {
                "basis_supplier_shelf", [
                    "item_event_price",
                    "begin_time",
                    "end_time",
                    "event_type",
                    "stack_flag",
                    "match_value",
                    "gift_number",
                    "gift_product_item_code",
                    "limit_sale_number",
                    "limit_sale_time",
                    "stock_number",
                    "safe_stock",
                    "sales_fee_rule",
                    "sales_fee_value",
                    "sales_fee_value2",
                    "sales_fee_value3",
                    "supplier_item_type",
                    "product_package_code",
                    "mt_price",
                    "xy_sale_product_id",
                    "xy_supplier_sale_product_id",
                    "xy_base_product_id"
                ]
            },
            {
                "basis_account_shop", [
                    "vip_expire_at",
                    "category_codes",
                    "schedule_visit_date",
                    "visit_interval_day",
                    "first_order_time",
                    "is_subscription",
                    "apple_iap_secret_code",
                    "profit_channel",
                    "xy_license_id"
                ]
            },
            {
                "basis_order_info", [
                    "account_channel_code",
                    "user_point",
                    "reminder_status",
                    "reminder_time",
                    "total_fee_service",
                    "total_fee_factory",
                    "total_fee_rebate",
                    "estimate_arrive_time",
                    "after_sale_total_refund",
                    "is_comment",
                    "bd_sales_price",
                    "total_fee_sales",
                    "total_fee_sales2",
                    "total_fee_sales3",
                    "supplier_item_type",
                    "warehouse_status",
                    "warehouse_push_result",
                    "shop_profit_channel",
                    "is_redeploy_detail",
                    "redeploy_detail_total_price",
                    "is_expiry_detail",
                    "expiry_detail_total_price"
                ]
            },
            {
                "basis_product_item", [
                    "product_usage",
                    "shelf_life_days_desc"
                ]
            },
            {
                "basis_order_detail", [
                    "order_item_type",
                    "event_json",
                    "fee_service",
                    "fee_factory",
                    "fee_rebate",
                    "fee_sales",
                    "fee_sales2",
                    "fee_sales3",
                    "refund_reason",
                    "platform_pay_refund",
                    "xy_sale_product_id"
                ]
            },
            {
                "basis_account_supplier",
                [
                    "supplier_type",
                    "supplier_address",
                    "address_city",
                    "address_lat",
                    "address_lng",
                    "address_province",
                    "address_region",
                    "xy_username",
                    "xy_payload",
                    "xy_tenant_id",
                    "xy_account_id"
                ]
            },
            {
                "basis_refund_order_detail",
                [
                    "account_supplier_code",
                    "refund_flag",
                    "pick_quantity"
                ]
            }
        };

        var tableList = dict.Select(a => a.Key).ToList();
        var n = entityModel.Metadata.Name?.Name;
        if (n != null && tableList.Contains(n))
        {
            var columns = dict.First(a => a.Key == n).Value;
            var filteredColumns = entityModel.Columns
                .Where(col => !columns.Contains(col.Property.Name,
                    StringComparer.OrdinalIgnoreCase))
                .ToList();
            // 更新列集合
            Console.WriteLine($"[FILTER] Table {entityModel.Metadata.Name} " +
                              $"col count: {entityModel.Columns.Count}, " +
                              $"Filtered count: {filteredColumns.Count}"
            );
            entityModel.Columns.Clear();
            entityModel.Columns.AddRange(filteredColumns);
        }

        base.PreprocessEntity(typeParser, entityModel);
    }
}