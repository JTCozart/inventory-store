# Quick Start Guide

This guide walks through the most common tasks in Inventory Tracker.

---

## 1. First login

After installation, open **http://localhost:5050** (or double-click the tray icon).

You will be prompted to create an admin account — enter a username and password (minimum 8 characters). This is a one-time setup; the page is not accessible once an account exists.

---

## 2. Add your first item

1. Click **Inventory** in the sidebar
2. Click **Add Item**
3. Choose a type:
   - **Reusable** — equipment that is checked out and returned (laptops, cameras, tools)
   - **Consumable** — stock that is depleted when used (batteries, paper, cable ties)
4. Fill in Name, Quantity, and optionally SKU, Location, and Minimum Quantity
5. Click **Save Item**

**Tip:** Set a Minimum Quantity to receive a Low Stock warning on the dashboard when stock falls to or below that level.

---

## 3. Check out a reusable item

**From the dashboard or inventory list:**
1. Click an item name to open its detail panel
2. Enter the name of the person checking it out
3. Set the quantity (default 1)
4. Click **Check Out**

**By scanning a barcode:**
1. Click **Scan** at the top of the dashboard
2. Aim the camera at the barcode, or type the SKU manually
3. The item details load automatically — click **Check Out**

---

## 4. Check an item back in

1. Open the item detail panel (click the item name anywhere)
2. Under **Active Checkouts**, find the person's record
3. Click the **Check In** button

---

## 5. Consume or restock a consumable item

1. Open the item detail panel
2. Enter quantity and optional notes
3. Click **Remove / Consume** or **Add / Restock**

---

## 6. Print barcode labels

1. Go to **Reports → Barcode Sheet**
2. Items with a SKU assigned will appear
3. Click **Print** — the page formats itself for printing with barcodes

To assign a SKU to an item, edit the item and enter a value in the **SKU / Barcode** field. You can scan an existing barcode into that field using the scan button next to the field.

---

## 7. Run a report

| Report | Where to find it |
|---|---|
| Stock levels (all items) | Reports → Stock Levels |
| Active checkouts | Reports → Checked Out |
| Lost items | Reports → Lost Items |
| Physical inventory sheet (printable) | Reports → Take Inventory |
| Full audit log | Reports → Activity Log |

The Activity Log supports date/time range filtering — use the **From** and **To** pickers and click **Apply**.

---

## 8. Add users

1. Go to **Settings → User Management** (Admin only)
2. Click **Add User**
3. Assign a role:
   - **Admin** — full access including user management and settings
   - **Manager** — can manage inventory and run reports, cannot manage users
   - **Viewer** — read-only access

---

## 9. Remote access

To let staff access Inventory Tracker from outside the office:

1. Go to **Settings → Remote Access**
2. Choose a tunnel type and click Start
3. Share the public URL with your team

The URL can be set to auto-start on service restart under **Autostart** options.

---

## 10. Reset the admin password

If you are locked out, right-click the tray icon and choose **Reset Admin Password**. This resets the first admin account's password without requiring a login.

---

## Tray icon menu

| Menu item | Action |
|---|---|
| Open Inventory Tracker | Opens the web UI in your default browser |
| Local: http://... | Shows the local network address (click to copy) |
| Tunnel: ... | Shows the tunnel URL if active (click to copy) |
| Reset Admin Password | Resets the admin password without logging in |
| Exit | Closes the tray companion (service keeps running) |
