# EAN Field Changes Summary

## What Changed?

The `SKU` field has been renamed to `EAN` (European Article Number) to better reflect its purpose as a barcode identifier.

## Key Improvements

### 1. **Proper EAN Extraction**
- EAN is now extracted from standard JSON-LD properties (in priority order):
  - `gtin13` - EAN-13 barcode (most common)
  - `gtin` - Generic GTIN identifier
  - `sku` - Fallback if used for barcode
  - `pid` - Product ID fallback (some sites use this for EAN)
- **Important**: EAN is only set when actual barcode data is found in the product metadata

### 2. **Separation from Article Number**
- **Before**: SKU was automatically set to Article Number when not found (`sku = articleNumber`)
- **After**: EAN remains `null` if no barcode data exists
- This provides a clear distinction:
  - **Article Number**: Internal product identifier (always populated)
  - **EAN**: External barcode identifier (only when available)

### 3. **Database Changes**
- Column renamed: `SKU` → `EAN`
- Existing data preserved during migration
- All references updated throughout the codebase

## Files Modified

1. **Product.cs** - Renamed `SKU` property to `EAN`
2. **ApplicationDbContext.cs** - Updated configuration for `EAN`
3. **ProductCrawlerService.cs** - Enhanced extraction logic:
   - Checks `gtin13`, `gtin`, `sku`, and `pid` JSON-LD properties
   - Removed automatic fallback to article number
   - Updated logging to show EAN extraction source
4. **Products.razor** - UI now displays "EAN" column instead of "SKU"
5. **FEATURE_SUMMARY.md** - Documentation updated

## Migration Details

**Migration Name**: `RenameSkuToEan`

**SQL Operation**:
```sql
EXEC sp_rename N'[Products].[SKU]', N'EAN', 'COLUMN';
```

This preserves all existing data while renaming the column.

## How to Use

When crawling products:

1. **If the product has barcode data** (gtin13/gtin/sku/pid in JSON-LD):
   - EAN field will be populated with the barcode
   - Example: `"EAN": "1234567890123"`

2. **If the product has no barcode data**:
   - EAN field will be `null`
   - Article Number will still be populated
   - Example: `"EAN": null, "ArticleNumber": "PROD-123"`

## Benefits

✅ **Accurate Data**: EAN only contains actual barcode numbers  
✅ **Clear Semantics**: Field name matches its purpose  
✅ **Standards Compliant**: Uses standard GTIN/EAN extraction from JSON-LD  
✅ **Flexible**: Supports products with or without barcodes  

## Example JSON-LD Extraction

### Example 1: Standard GTIN
```json
{
  "@type": "Product",
  "name": "T-Shirt",
  "gtin13": "1234567890123",  // ✓ Extracted as EAN
  "productID": "TSHIRT-001",   // ✓ Extracted as Article Number
  "sku": "VARIANT-XL-BLUE"     // ✗ Skipped (gtin13 takes priority)
}
```
- **EAN**: `"1234567890123"` (from gtin13)
- **Article Number**: `"TSHIRT-001"` (from productID)

### Example 2: Using PID fallback
```json
{
  "@type": "Product",
  "name": "Shoe",
  "pid": "8901234567890",      // ✓ Extracted as EAN (no gtin13/gtin/sku)
  "productID": "SHOE-456"      // ✓ Extracted as Article Number
}
```
- **EAN**: `"8901234567890"` (from pid)
- **Article Number**: `"SHOE-456"` (from productID)

## Testing

To verify the changes:

1. Run the application
2. Crawl a product page
3. Check the logs for:
   ```
   ✓ EAN (gtin13): 1234567890123
   ```
4. View the Products page - EAN column should show barcode numbers (or be empty if none found)
5. Verify Article Number and EAN are different values (when EAN exists)
