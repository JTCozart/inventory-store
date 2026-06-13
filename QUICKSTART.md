# Quick Start Guide

This guide covers the most common tasks. For full documentation see the [User Guide](https://inventorystore.app/user-guide.html).

---

## 1. First login

After installation, open **http://localhost:5050** or double-click the tray icon.

You will be prompted to create an admin account - enter a username and password (minimum 8 characters). This is a one-time setup; the page disappears once an account exists.

---

## 2. Create categories (optional but recommended)

Categories let you group and filter items (e.g. Tools, Electronics, Safety).

1. Go to **Settings → Categories**
2. Click **Add Category**, enter a name and pick a color
3. Categories are then available when creating or editing items

---

## 3. Add your first item

1. Click **Inventory** in the sidebar
2. Click **Add Item**
3. Choose a type:
   - **Reusable** - equipment that is checked out and returned (laptops, cameras, tools)
   - **Consumable** - stock that is depleted when used (batteries, paper, cable ties)
   - You can change an item's type later. Open the item, click **Edit**, and use the **Convert to ...** link under the type badge. For a reusable item, check in all units first.
4. Fill in the fields:
   - **Name** - required
   - **Quantity** - total units you have
   - **Minimum Quantity** - triggers a Low Stock warning when available stock reaches this level
   - **SKU / Barcode** - scan with camera or type manually; used for barcode labels and scanning
     - Optional: Click the **Lookup** button (magnifying glass) next to SKU to pull product details automatically - name, image, brand, category, size, and weight (works with retail barcodes, ISBN, and QR codes)
   - **Location** - shelf, bin, or room
   - **Category** - optional group (created in Settings)
   - **Expiry Date** - optional; items show Expired / Expiring Soon badges when due
   - **Scan Warning** - message shown whenever this item is scanned (e.g. "Requires PPE")
   - **Description** - free-text notes
5. Click **Save Item**

---

## 4. Quick-add an item while scanning

When you scan a barcode and it is not in your inventory:

1. Click the **Scan** button in the navigation bar
2. Scan the barcode with your camera
3. If the item is not found, click **Add Item**
4. The SKU is pre-filled. Enter a name or click **Lookup** next to the SKU to search for the product details
5. Fill in quantity, type, and location
6. Click **Add & Continue**

The item is added and the scanner resets for the next barcode.

---

## 5. Check out a reusable item

**From the inventory list:**
1. Click an item row to open its detail panel
2. Enter the person's name, quantity, and optional notes
3. Click **Check Out**

**By scanning a barcode:**
1. Navigate to the Inventory page
2. Use the Scan button or navigate directly to the item by scanning
3. The item detail loads - click **Check Out**

---

## 6. Check an item back in

1. Open the item detail panel
2. Under **Active Checkouts**, find the person's record
3. Click **Check In** (or **Mark Lost** if the item was not returned)

---

## 7. Consume or restock a consumable item

1. Open the item detail panel
2. In the **Adjust Stock** control, set the quantity and optional notes
3. Click the **−** button to consume (use) stock, or the **+** button to add stock

The **In Stock** number briefly flashes red (consumed) or green (added) to confirm the change.

---

## 8. Import items from a CSV

To bulk-load your inventory from a spreadsheet:

1. Click **Inventory** → **Import** button (top right)
2. Prepare a CSV with this header row:
   ```
   Type,Name,Quantity,MinimumQuantity,SKU,Location,Category,ExpiryDate,Description,ScanWarning
   ```
   - **Type** must be `Reusable` or `Consumable`
   - **ExpiryDate** format: `YYYY-MM-DD` (e.g. `2027-03-01`)
   - Categories are created automatically if they don't exist
3. Upload the file and click **Import**

To export your current inventory to CSV, go to **Reports** and click **Export CSV**.

---

## 9. Track expiry dates

Items with expiry dates show colored badges in the inventory list:
- **Expired** (red) - expiry date has passed
- **Exp MMM DD** (yellow) - expiring within 30 days

For a full expiry overview go to **Reports → Expiry**:
- All expired items listed in red
- Items expiring within 90 days, sorted by date

---

## 10. Print barcode labels

1. Go to **Reports → Barcode Sheet**
2. Items with a SKU appear automatically
3. Click **Print**

To assign a SKU, edit the item and type or scan into the **SKU / Barcode** field.

---

## 11. Run a report

| Report | Location |
|---|---|
| Stock levels (all items + expiry) | Reports → Stock Levels |
| Active checkouts | Reports → Checked Out |
| Lost items | Reports → Lost Items |
| Physical inventory checklist | Reports → Take Inventory |
| Expired / expiring items | Reports → Expiry |
| Barcode sheet | Reports → Barcode Sheet |
| Full audit trail | Reports → Activity Log |

Click **Export CSV** (top of Reports page) to download all items as a spreadsheet.

---

## 12. Add users

1. Go to **Settings → User Management** (Admin only)
2. Click **Add User**
3. Assign a role:
   - **Admin** - full access including user management and settings
   - **Manager** - manage inventory and run reports; cannot manage users or settings
   - **Staff** - the Terminal only; can check out, check in, and consume, but cannot restock, add items, or open reports and settings
   - **Viewer** - read-only access

---

## 13. Remote access (Serveo - recommended)

To let staff access Inventory Store from outside the office:

1. Go to **Settings → Remote Access → Serveo**
2. Click **Generate SSH Key** - the app creates a key pair stored in the database
3. Copy the public key and paste it into your account at [console.serveo.net](https://console.serveo.net) under **SSH Keys**
4. Choose a subdomain (e.g. `myinventory` → `https://myinventory.serveousercontent.com`)
5. Click **Save Subdomain**
6. Click the green **Serveo** button to start the tunnel
7. To start automatically on reboot, go to **Auto-start** and select **Serveo**

**Note:** The free Serveo tier shows a visitor interstitial page. Upgrade to Serveo Pro ($6/mo) to remove it.

---

## 14. Reset the admin password

If locked out, right-click the tray icon and choose **Reset Admin Password**.

---

## 15. Turn on optional modules

Inventory Store has optional add-ons under **Settings → Modules** (Admin only). Each one is off by default and stays out of the way until you switch it on, then click **Configure** to manage it:

- **Safety Data Sheets** - keep chemical hazard info (signal word, GHS pictograms, CAS number) on items, looked up free from PubChem
- **Cost & Valuation** - record a unit cost per item and see total inventory value, by category, with depreciation (also a Valuation report)
- **Consumption Forecasting** - projects when consumables will run out from real usage history (also a Forecast report)
- **Webhooks & Integrations** - send signed messages to a URL on inventory events, to connect Slack, Zapier, Make, or your own scripts

**Example - Safety Data Sheets:** switch it on, open an item, click **Edit**, enter the chemical name (e.g. `acetone`) in the **Safety Data Sheet** section, and click **Look up SDS**. The hazard pictograms then appear on the item and scan cards.

See the [User Guide](https://inventorystore.app/user-guide.html) for full details on each module.

> Tip: For SDS, PubChem lists chemicals, not brand names. Search the chemical (e.g. "sodium hypochlorite") rather than a product name (e.g. "bleach"). If there is no exact match, the app suggests close chemical names to pick from.

---

## 16. Use the Terminal on a shared tablet

The Terminal is a simple, full-screen page with big buttons for a tablet or front desk. Open **http://localhost:5050/terminal** (or use your tunnel address).

1. Type a SKU or part of a name in the search box - matches appear as you type - or tap **Scan with camera**
2. Pick the item to open it
3. For a reusable item, enter the borrower and tap **Check Out**, or tap **Check In** on a row to take it back
4. For a consumable, tap the red **−** to use stock, or the green **+** to add stock (Admins and Managers only)

If an item has none left, a red **Out of stock** message shows and the button is turned off.

Tip: create a **Staff** user (see step 12) for front-desk people. Staff log straight into the Terminal and have just the buttons they need. Admins and Managers can open the Terminal any time from the tablet icon in the top bar and use the **home** button to return to the main app.

---

## Tray icon menu

| Item | Action |
|---|---|
| Open Inventory Store | Opens the web UI in your default browser |
| Local: http://... | Shows the local network address - click to copy |
| Tunnel: ... | Shows the active tunnel URL - click to copy |
| Reset Admin Password | Resets the admin password without logging in |
| Exit | Closes the tray companion (service keeps running) |
