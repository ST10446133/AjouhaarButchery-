using AjouhaarSalesStockWebsite.Models;
using MySqlConnector;

namespace AjouhaarSalesStockWebsite.Data;

public class ButcheryDataStore
{
    private readonly string _connectionString;

    public ButcheryDataStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("ButcheryDatabase")
            ?? throw new InvalidOperationException("Missing ButcheryDatabase connection string.");
    }

    public List<User> Users
    {
        get
        {
            using MySqlConnection connection = OpenConnection();
            using MySqlCommand command = new(
                "SELECT user_id, full_name, role, username FROM users ORDER BY user_id",
                connection);

            using MySqlDataReader reader = command.ExecuteReader();
            List<User> users = [];

            while (reader.Read())
            {
                users.Add(new User
                {
                    UserID = reader.GetInt32("user_id"),
                    FullName = reader.GetString("full_name"),
                    Role = reader.GetString("role"),
                    Username = reader.GetString("username")
                });
            }

            return users;
        }
    }

    public List<Product> Products
    {
        get
        {
            using MySqlConnection connection = OpenConnection();
            using MySqlCommand command = new(
                @"SELECT product_id, product_name, category, unit_price, quantity_on_hand,
                         reorder_level, is_active, last_updated
                  FROM products
                  ORDER BY product_id",
                connection);

            using MySqlDataReader reader = command.ExecuteReader();
            List<Product> products = [];

            while (reader.Read())
            {
                products.Add(ReadProduct(reader));
            }

            return products;
        }
    }

    public List<Sale> Sales
    {
        get
        {
            using MySqlConnection connection = OpenConnection();
            using MySqlCommand command = new(
                @"SELECT s.sale_id, s.receipt_number, c.name AS customer_name, c.contact_number,
                         u.full_name AS cashier, s.sale_date, s.payment_method
                  FROM sales s
                  INNER JOIN customers c ON c.customer_id = s.customer_id
                  INNER JOIN users u ON u.user_id = s.user_id
                  ORDER BY s.sale_date",
                connection);

            using MySqlDataReader reader = command.ExecuteReader();
            List<Sale> sales = [];

            while (reader.Read())
            {
                sales.Add(new Sale
                {
                    SaleID = reader.GetInt32("sale_id"),
                    ReceiptNumber = reader.GetString("receipt_number"),
                    CustomerName = reader.GetString("customer_name"),
                    CustomerContact = reader.GetString("contact_number"),
                    Cashier = reader.GetString("cashier"),
                    SaleDate = reader.GetDateTime("sale_date"),
                    PaymentMethod = reader.GetString("payment_method")
                });
            }

            reader.Close();

            foreach (Sale sale in sales)
            {
                sale.Items = GetSaleItems(connection, sale.SaleID);
            }

            return sales;
        }
    }

    public User? ValidateLogin(string username, string password, string role)
    {
        using MySqlConnection connection = OpenConnection();
        using MySqlCommand command = new(
            @"SELECT user_id, full_name, role, username, password_hash
              FROM users
              WHERE username = @username AND role = @role
              LIMIT 1",
            connection);

        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@role", role);

        using MySqlDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        string passwordHash = reader.GetString("password_hash").Replace("$2y$", "$2a$");
        if (!BCrypt.Net.BCrypt.Verify(password, passwordHash))
        {
            return null;
        }

        return new User
        {
            UserID = reader.GetInt32("user_id"),
            FullName = reader.GetString("full_name"),
            Role = reader.GetString("role"),
            Username = reader.GetString("username")
        };
    }

    public Product? FindProduct(int productID)
    {
        using MySqlConnection connection = OpenConnection();
        using MySqlCommand command = new(
            @"SELECT product_id, product_name, category, unit_price, quantity_on_hand,
                     reorder_level, is_active, last_updated
              FROM products
              WHERE product_id = @productID",
            connection);

        command.Parameters.AddWithValue("@productID", productID);

        using MySqlDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadProduct(reader) : null;
    }

    public void SaveProduct(Product product)
    {
        using MySqlConnection connection = OpenConnection();

        if (product.ProductID == 0)
        {
            using MySqlCommand insertCommand = new(
                @"INSERT INTO products (product_name, category, unit_price, quantity_on_hand, reorder_level, is_active, last_updated)
                  VALUES (@productName, @category, @unitPrice, @quantityOnHand, @reorderLevel, @isActive, @lastUpdated)",
                connection);
            AddProductParameters(insertCommand, product);
            insertCommand.ExecuteNonQuery();
            return;
        }

        using MySqlCommand updateCommand = new(
            @"UPDATE products
              SET product_name = @productName,
                  category = @category,
                  unit_price = @unitPrice,
                  quantity_on_hand = @quantityOnHand,
                  reorder_level = @reorderLevel,
                  is_active = @isActive,
                  last_updated = @lastUpdated
              WHERE product_id = @productID",
            connection);
        updateCommand.Parameters.AddWithValue("@productID", product.ProductID);
        AddProductParameters(updateCommand, product);
        updateCommand.ExecuteNonQuery();
    }

    public bool UsernameExists(string username)
    {
        using MySqlConnection connection = OpenConnection();
        using MySqlCommand command = new(
            "SELECT COUNT(*) FROM users WHERE username = @username",
            connection);
        command.Parameters.AddWithValue("@username", username);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public bool UsernameExists(string username, int excludedUserID)
    {
        using MySqlConnection connection = OpenConnection();
        using MySqlCommand command = new(
            "SELECT COUNT(*) FROM users WHERE username = @username AND user_id <> @userID",
            connection);
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@userID", excludedUserID);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public void AddUser(User user)
    {
        using MySqlConnection connection = OpenConnection();
        using MySqlCommand command = new(
            @"INSERT INTO users (full_name, role, username, password_hash)
              VALUES (@fullName, @role, @username, @passwordHash)",
            connection);
        command.Parameters.AddWithValue("@fullName", user.FullName);
        command.Parameters.AddWithValue("@role", user.Role);
        command.Parameters.AddWithValue("@username", user.Username);
        command.Parameters.AddWithValue("@passwordHash", BCrypt.Net.BCrypt.HashPassword(user.Password));
        command.ExecuteNonQuery();
    }

    public User? FindUser(int userID)
    {
        using MySqlConnection connection = OpenConnection();
        using MySqlCommand command = new(
            "SELECT user_id, full_name, role, username FROM users WHERE user_id = @userID",
            connection);
        command.Parameters.AddWithValue("@userID", userID);

        using MySqlDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new User
        {
            UserID = reader.GetInt32("user_id"),
            FullName = reader.GetString("full_name"),
            Role = reader.GetString("role"),
            Username = reader.GetString("username")
        };
    }

    public void UpdateUser(User user)
    {
        using MySqlConnection connection = OpenConnection();

        if (string.IsNullOrWhiteSpace(user.Password))
        {
            using MySqlCommand command = new(
                @"UPDATE users
                  SET full_name = @fullName,
                      role = @role,
                      username = @username
                  WHERE user_id = @userID",
                connection);
            AddUserUpdateParameters(command, user);
            command.ExecuteNonQuery();
            return;
        }

        using MySqlCommand passwordCommand = new(
            @"UPDATE users
              SET full_name = @fullName,
                  role = @role,
                  username = @username,
                  password_hash = @passwordHash
              WHERE user_id = @userID",
            connection);
        AddUserUpdateParameters(passwordCommand, user);
        passwordCommand.Parameters.AddWithValue("@passwordHash", BCrypt.Net.BCrypt.HashPassword(user.Password));
        passwordCommand.ExecuteNonQuery();
    }

    public void DeleteUser(int userID)
    {
        using MySqlConnection connection = OpenConnection();
        using MySqlCommand command = new(
            "DELETE FROM users WHERE user_id = @userID",
            connection);
        command.Parameters.AddWithValue("@userID", userID);
        command.ExecuteNonQuery();
    }

    public Sale CompleteSale(string customerName, string customerContact, string cashier, string paymentMethod, List<SaleItem> items)
    {
        using MySqlConnection connection = OpenConnection();
        using MySqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int customerID = GetOrCreateCustomer(connection, transaction, customerName, customerContact);
            int userID = GetUserIDByFullName(connection, transaction, cashier);
            int saleID = GetNextSaleID(connection, transaction);
            string receiptNumber = $"REC-{saleID:000000}";
            DateTime saleDate = DateTime.Now;
            decimal totalAmount = items.Sum(item => item.LineTotal);

            using MySqlCommand saleCommand = new(
                @"INSERT INTO sales (sale_id, receipt_number, customer_id, user_id, sale_date, total_amount, payment_method)
                  VALUES (@saleID, @receiptNumber, @customerID, @userID, @saleDate, @totalAmount, @paymentMethod)",
                connection,
                transaction);
            saleCommand.Parameters.AddWithValue("@saleID", saleID);
            saleCommand.Parameters.AddWithValue("@receiptNumber", receiptNumber);
            saleCommand.Parameters.AddWithValue("@customerID", customerID);
            saleCommand.Parameters.AddWithValue("@userID", userID);
            saleCommand.Parameters.AddWithValue("@saleDate", saleDate);
            saleCommand.Parameters.AddWithValue("@totalAmount", totalAmount);
            saleCommand.Parameters.AddWithValue("@paymentMethod", paymentMethod);
            saleCommand.ExecuteNonQuery();

            foreach (SaleItem item in items)
            {
                InsertSaleItem(connection, transaction, saleID, item);
                ReduceStock(connection, transaction, item);
            }

            using MySqlCommand receiptCommand = new(
                @"INSERT INTO receipts (sale_id, receipt_number, printed_date)
                  VALUES (@saleID, @receiptNumber, @printedDate)",
                connection,
                transaction);
            receiptCommand.Parameters.AddWithValue("@saleID", saleID);
            receiptCommand.Parameters.AddWithValue("@receiptNumber", receiptNumber);
            receiptCommand.Parameters.AddWithValue("@printedDate", saleDate);
            receiptCommand.ExecuteNonQuery();

            transaction.Commit();

            return new Sale
            {
                SaleID = saleID,
                ReceiptNumber = receiptNumber,
                CustomerName = string.IsNullOrWhiteSpace(customerName) ? "Walk-in Customer" : customerName,
                CustomerContact = string.IsNullOrWhiteSpace(customerContact) ? "N/A" : customerContact,
                Cashier = cashier,
                SaleDate = saleDate,
                PaymentMethod = paymentMethod,
                Items = items
            };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private MySqlConnection OpenConnection()
    {
        MySqlConnection connection = new(_connectionString);
        connection.Open();
        return connection;
    }

    private static Product ReadProduct(MySqlDataReader reader)
    {
        return new Product
        {
            ProductID = reader.GetInt32("product_id"),
            ProductName = reader.GetString("product_name"),
            Category = reader.GetString("category"),
            UnitPrice = reader.GetDecimal("unit_price"),
            QuantityOnHand = reader.GetDecimal("quantity_on_hand"),
            ReorderLevel = reader.GetDecimal("reorder_level"),
            IsActive = reader.GetBoolean("is_active"),
            LastUpdated = DateOnly.FromDateTime(reader.GetDateTime("last_updated"))
        };
    }

    private static void AddProductParameters(MySqlCommand command, Product product)
    {
        command.Parameters.AddWithValue("@productName", product.ProductName);
        command.Parameters.AddWithValue("@category", product.Category);
        command.Parameters.AddWithValue("@unitPrice", product.UnitPrice);
        command.Parameters.AddWithValue("@quantityOnHand", product.QuantityOnHand);
        command.Parameters.AddWithValue("@reorderLevel", product.ReorderLevel);
        command.Parameters.AddWithValue("@isActive", product.IsActive);
        command.Parameters.AddWithValue("@lastUpdated", product.LastUpdated.ToDateTime(TimeOnly.MinValue));
    }

    private static void AddUserUpdateParameters(MySqlCommand command, User user)
    {
        command.Parameters.AddWithValue("@userID", user.UserID);
        command.Parameters.AddWithValue("@fullName", user.FullName);
        command.Parameters.AddWithValue("@role", user.Role);
        command.Parameters.AddWithValue("@username", user.Username);
    }

    private static List<SaleItem> GetSaleItems(MySqlConnection connection, int saleID)
    {
        using MySqlCommand command = new(
            @"SELECT si.product_id, p.product_name, si.quantity_sold, si.unit_price
              FROM sale_items si
              INNER JOIN products p ON p.product_id = si.product_id
              WHERE si.sale_id = @saleID",
            connection);
        command.Parameters.AddWithValue("@saleID", saleID);

        using MySqlDataReader reader = command.ExecuteReader();
        List<SaleItem> items = [];

        while (reader.Read())
        {
            items.Add(new SaleItem
            {
                ProductID = reader.GetInt32("product_id"),
                ProductName = reader.GetString("product_name"),
                QuantitySold = reader.GetDecimal("quantity_sold"),
                UnitPrice = reader.GetDecimal("unit_price")
            });
        }

        return items;
    }

    private static int GetOrCreateCustomer(MySqlConnection connection, MySqlTransaction transaction, string customerName, string customerContact)
    {
        string name = string.IsNullOrWhiteSpace(customerName) ? "Walk-in Customer" : customerName;
        string contact = string.IsNullOrWhiteSpace(customerContact) ? "N/A" : customerContact;

        using MySqlCommand findCommand = new(
            @"SELECT customer_id
              FROM customers
              WHERE name = @name AND contact_number = @contact
              LIMIT 1",
            connection,
            transaction);
        findCommand.Parameters.AddWithValue("@name", name);
        findCommand.Parameters.AddWithValue("@contact", contact);

        object? existingID = findCommand.ExecuteScalar();
        if (existingID != null)
        {
            return Convert.ToInt32(existingID);
        }

        using MySqlCommand insertCommand = new(
            "INSERT INTO customers (name, contact_number) VALUES (@name, @contact); SELECT LAST_INSERT_ID();",
            connection,
            transaction);
        insertCommand.Parameters.AddWithValue("@name", name);
        insertCommand.Parameters.AddWithValue("@contact", contact);
        return Convert.ToInt32(insertCommand.ExecuteScalar());
    }

    private static int GetUserIDByFullName(MySqlConnection connection, MySqlTransaction transaction, string fullName)
    {
        using MySqlCommand command = new(
            "SELECT user_id FROM users WHERE full_name = @fullName LIMIT 1",
            connection,
            transaction);
        command.Parameters.AddWithValue("@fullName", fullName);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static int GetNextSaleID(MySqlConnection connection, MySqlTransaction transaction)
    {
        using MySqlCommand command = new(
            "SELECT COALESCE(MAX(sale_id), 300) + 1 FROM sales",
            connection,
            transaction);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void InsertSaleItem(MySqlConnection connection, MySqlTransaction transaction, int saleID, SaleItem item)
    {
        using MySqlCommand command = new(
            @"INSERT INTO sale_items (sale_id, product_id, quantity_sold, unit_price, line_total)
              VALUES (@saleID, @productID, @quantitySold, @unitPrice, @lineTotal)",
            connection,
            transaction);
        command.Parameters.AddWithValue("@saleID", saleID);
        command.Parameters.AddWithValue("@productID", item.ProductID);
        command.Parameters.AddWithValue("@quantitySold", item.QuantitySold);
        command.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
        command.Parameters.AddWithValue("@lineTotal", item.LineTotal);
        command.ExecuteNonQuery();
    }

    private static void ReduceStock(MySqlConnection connection, MySqlTransaction transaction, SaleItem item)
    {
        using MySqlCommand command = new(
            @"UPDATE products
              SET quantity_on_hand = quantity_on_hand - @quantitySold,
                  last_updated = @lastUpdated
              WHERE product_id = @productID",
            connection,
            transaction);
        command.Parameters.AddWithValue("@quantitySold", item.QuantitySold);
        command.Parameters.AddWithValue("@lastUpdated", DateTime.Today);
        command.Parameters.AddWithValue("@productID", item.ProductID);
        command.ExecuteNonQuery();
    }
}
