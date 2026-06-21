# Ajouhaar Sales and Stock Website

This is the sales and stock management website for Ajouhaar Moslem Butchery. It was built with ASP.NET Core Razor Pages and connects to a local MySQL database called `ajouhaar_butchery`.

## How to Run the Website

1. Open `AjouhaarSalesStockWebsite.sln` in Visual Studio.
2. Make sure XAMPP MySQL is running.
3. Press `F5` or use the run button in Visual Studio.
4. Log in with one of the test accounts.

Test accounts:

- Manager: `sajouhaar` / `1234`
- Cashier: `cashier1` / `1234`
- Cashier: `cashier2` / `1234`

## What the Website Does

The system allows staff to:

- log in as a manager or cashier
- capture a new sale
- reduce stock after a sale is completed
- print customer receipts
- view products and stock levels
- search transaction history
- print stock, transaction and sales reports

The manager can also:

- add new products
- mark products as active or inactive
- add staff members
- update staff details
- delete staff members if they are not linked to existing sales

## Database

The database files are in the `database` folder.

Tables used:

- `users`
- `products`
- `customers`
- `sales`
- `sale_items`
- `receipts`

The website connects to phpMyAdmin using the connection string in `appsettings.json`.

Passwords are not saved as plain text. They are saved as password hashes and checked when the user logs in.
