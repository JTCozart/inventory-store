# Quick Start Guide

This guide covers the most common tasks. For full documentation see the [User Guide](https://jtcozart.github.io/inventory-tracker/user-guide.html).

---

## 1. First login

After installation, open **http://localhost:5050** or double-click the tray icon.

You will be prompted to create an admin account — enter a username and password (minimum 8 characters). This is a one-time setup; the page disappears once an account exists.

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
   - **Reusable** — equipment that is checked out and returned (laptops, cameras, tools)
   - **Consumable** — stock that is depleted when used (batteries, paper, cable ties)
4. Fill in the fields:
   - **Name** — required
   - **Quantity** — total units you have
   - **Minimum Quantity** — triggers a Low Stock warning when available stock reaches this level
   - **SKU / Barcode** — scan with camera or type manually; used for barcode labels and scanning
   - **Location** — shelf, bin, or room
   - **Category** — optional group (created in Settings)
   - **Expiry Date** — optional; items show Expired / Expiring Soon badges when due
   - **Scan Warning** — message shown whenever this item is scanned (e.g. "Requires PPE")
   - **Description** — free-text notes
5. Click **Save Item**

---

## 4. Check out a reusable item

**From the inventory list:**
1. Click an item row to open its detail panel
2. Enter the person's name, quantity, and optional notes
3. Click **Check Out**

**By scanning a barcode:**
1. Navigate to the Inventory page
2. Use the Import/scan button or navigate directly to the item by scanning
3. The item detail loads — click **Check Out**

---

## 5. Check an item back in

1. Open the item detail panel
2. Under **Active Checkouts**, find the person's record
3. Click **Check In** (or **Mark Lost** if the item was not returned)

---

## 6. Consume or restock a consumable item

1. Open the item detail panel
2. Enter the quantity and optional notes
3. Click **Consume** to decrement stock, or **Restock** to add stock

---

## 7. Import items from a CSV

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

## 8. Track expiry dates

Items with expiry dates show colored badges in the inventory list:
- **Expired** (red) — expiry date has passed
- **Exp MMM DD** (yellow) — expiring within 30 days

For a full expiry overview go to **Reports → Expiry**:
- All expired items listed in red
- Items expiring within 90 days, sorted by date

---

## 9. Print barcode labels

1. Go to **Reports → Barcode Sheet**
2. Items with a SKU appear automatically
3. Click **Print**

To assign a SKU, edit the item and type or scan into the **SKU / Barcode** field.

---

## 10. Run a report

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

## 11. Add users

1. Go to **Settings → User Management** (Admin only)
2. Click **Add User**
3. Assign a role:
   - **Admin** — full access including user management and settings
   - **Manager** — manage inventory and run reports; cannot manage users or settings
   - **Viewer** — read-only access

---

## 12. Remote access (Serveo — recommended)

To let staff access Inventory Tracker from outside the office:

1. Go to **Settings → Remote Access → Serveo**
2. Click **Generate SSH Key** — the app creates a key pair stored in the database
3. Copy the public key and paste it into your account at [console.serveo.net](https://console.serveo.net) under **SSH Keys**
4. Choose a subdomain (e.g. `myinventory` → `https://myinventory.serveousercontent.com`)
5. Click **Save Subdomain**
6. Click the green **Serveo** button to start the tunnel
7. To start automatically on reboot, go to **Auto-start** and select **Serveo**

**Note:** The free Serveo tier shows a visitor interstitial page. Upgrade to Serveo Pro ($6/mo) to remove it.

---

## 13. Reset the admin password

If locked out, right-click the tray icon and choose **Reset Admin Password**.

---

## Tray icon menu

| Item | Action |
|---|---|
| Open Inventory Tracker | Opens the web UI in your default browser |
| Local: http://... | Shows the local network address — click to copy |
| Tunnel: ... | Shows the active tunnel URL — click to copy |
| Reset Admin Password | Resets the admin password without logging in |
| Exit | Closes the tray companion (service keeps running) |
