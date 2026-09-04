using System.Text.Json;
using AjouhaarSalesStockWebsite.Data;
using AjouhaarSalesStockWebsite.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace AjouhaarSalesStockWebsite.Pages;

public class IndexModel : PageModel
{
    private const string UserNameSessionKey = "ActiveUserName";
    private const string UserRoleSessionKey = "ActiveUserRole";
    private const string SaleItemsSessionKey = "CurrentSaleItems";

    private readonly ButcheryDataStore _dataStore;

    public IndexModel(ButcheryDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    [BindProperty]
    public LoginInput Login { get; set; } = new();

    [BindProperty]
    public SaleInput SaleForm { get; set; } = new();

    [BindProperty]
    public ProductInput ProductForm { get; set; } = new();

    [BindProperty]
    public StaffInput StaffForm { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string View { get; set; } = "Dashboard";

    [BindProperty(SupportsGet = true)]
    public string? SearchText { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? FilterDate { get; set; }

    public string? Message { get; set; }
    public string? MessageType { get; set; }
    public Sale? LastReceipt { get; set; }

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(ActiveUserName);
    public string ActiveUserName => HttpContext.Session.GetString(UserNameSessionKey) ?? string.Empty;
    public string ActiveUserRole => HttpContext.Session.GetString(UserRoleSessionKey) ?? string.Empty;
    public List<Product> Products => _dataStore.Products;
    public List<Product> ActiveProducts => _dataStore.Products.Where(product => product.IsActive).ToList();
    public List<Product> LowStockProducts => _dataStore.Products.Where(product => product.IsLowStock).ToList();
    public List<Sale> Sales => _dataStore.Sales;
    public List<User> Users => _dataStore.Users;
    public List<SaleItem> CurrentSaleItems => GetCurrentSaleItems();

    public decimal TodaySalesTotal => _dataStore.Sales
        .Where(sale => DateOnly.FromDateTime(sale.SaleDate) == DateOnly.FromDateTime(DateTime.Today))
        .Sum(sale => sale.TotalAmount);

    public decimal AllSalesTotal => _dataStore.Sales.Sum(sale => sale.TotalAmount);
    public decimal CashSalesTotal => _dataStore.Sales.Where(sale => sale.PaymentMethod == "Cash").Sum(sale => sale.TotalAmount);
    public decimal CardSalesTotal => _dataStore.Sales.Where(sale => sale.PaymentMethod == "Card").Sum(sale => sale.TotalAmount);

    public List<Sale> FilteredSales
    {
        get
        {
            IEnumerable<Sale> query = _dataStore.Sales;

            if (FilterDate.HasValue)
            {
                query = query.Where(sale => DateOnly.FromDateTime(sale.SaleDate) == FilterDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(sale =>
                    sale.ReceiptNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || sale.CustomerName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || sale.Cashier.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            return query.OrderByDescending(sale => sale.SaleDate).ToList();
        }
    }

    public void OnGet()
    {
        Message = TempData["Message"] as string;
        MessageType = TempData["MessageType"] as string;

        if (TempData["LastReceipt"] is string receiptJson)
        {
            LastReceipt = JsonSerializer.Deserialize<Sale>(receiptJson);
        }
    }

    public IActionResult OnPostLogin()
    {
        User? user = _dataStore.ValidateLogin(Login.Username, Login.Password, Login.Role);
        if (user == null)
        {
            SetTempMessage("Incorrect username or password.", "error");
            return RedirectToPage(new { View = "Dashboard" });
        }

        HttpContext.Session.SetString(UserNameSessionKey, user.FullName);
        HttpContext.Session.SetString(UserRoleSessionKey, user.Role);
        return RedirectToPage(new { View = "Dashboard" });
    }

    public IActionResult OnPostLogout()
    {
        HttpContext.Session.Clear();
        return RedirectToPage();
    }

    public IActionResult OnPostAddSaleItem()
    {
        if (!IsLoggedIn)
        {
            return RedirectToPage();
        }

        Product? product = _dataStore.FindProduct(SaleForm.ProductID);
        if (product == null || !product.IsActive)
        {
            SetTempMessage("Please select an active product.", "error");
            return RedirectToPage(new { View = "Sales" });
        }

        if (SaleForm.QuantitySold <= 0)
        {
            SetTempMessage("Quantity sold must be greater than zero.", "error");
            return RedirectToPage(new { View = "Sales" });
        }

        List<SaleItem> cart = GetCurrentSaleItems();
        decimal quantityAlreadyInCart = cart
            .Where(item => item.ProductID == product.ProductID)
            .Sum(item => item.QuantitySold);

        if (SaleForm.QuantitySold + quantityAlreadyInCart > product.QuantityOnHand)
        {
            SetTempMessage("Sale quantity cannot be more than available stock.", "error");
            return RedirectToPage(new { View = "Sales" });
        }

        cart.Add(new SaleItem
        {
            ProductID = product.ProductID,
            ProductName = product.ProductName,
            QuantitySold = SaleForm.QuantitySold,
            UnitPrice = product.UnitPrice
        });

        SaveCurrentSaleItems(cart);
        return RedirectToPage(new { View = "Sales" });
    }

    public IActionResult OnPostRemoveSaleItem(int index)
    {
        List<SaleItem> cart = GetCurrentSaleItems();
        if (index >= 0 && index < cart.Count)
        {
            cart.RemoveAt(index);
            SaveCurrentSaleItems(cart);
        }

        return RedirectToPage(new { View = "Sales" });
    }

    public IActionResult OnPostCompleteSale()
    {
        if (!IsLoggedIn)
        {
            return RedirectToPage();
        }

        List<SaleItem> cart = GetCurrentSaleItems();
        if (cart.Count == 0)
        {
            SetTempMessage("Add at least one product before completing the sale.", "error");
            return RedirectToPage(new { View = "Sales" });
        }

        Sale sale = _dataStore.CompleteSale(
            SaleForm.CustomerName,
            SaleForm.CustomerContact,
            ActiveUserName,
            SaleForm.PaymentMethod,
            cart);

        SaveCurrentSaleItems([]);
        TempData["LastReceipt"] = JsonSerializer.Serialize(sale);
        return RedirectToPage(new { View = "Sales" });
    }

    public IActionResult OnPostSaveProduct()
    {
        if (!IsLoggedIn || ActiveUserRole != "Manager")
        {
            SetTempMessage("Only the manager can save products.", "error");
            return RedirectToPage(new { View = "Products" });
        }

        if (string.IsNullOrWhiteSpace(ProductForm.ProductName) || ProductForm.UnitPrice <= 0)
        {
            SetTempMessage("Product name and unit price are required.", "error");
            return RedirectToPage(new { View = "Products" });
        }

        Product product = new()
        {
            ProductID = ProductForm.ProductID,
            ProductName = ProductForm.ProductName,
            Category = ProductForm.Category,
            UnitPrice = ProductForm.UnitPrice,
            QuantityOnHand = ProductForm.QuantityOnHand,
            ReorderLevel = ProductForm.ReorderLevel,
            IsActive = ProductForm.IsActive,
            LastUpdated = DateOnly.FromDateTime(DateTime.Today)
        };

        _dataStore.SaveProduct(product);
        return RedirectToPage(new { View = "Products" });
    }

    public IActionResult OnPostUpdateProductStatus(int productID, bool isActive)
    {
        if (!IsLoggedIn || ActiveUserRole != "Manager")
        {
            SetTempMessage("Only the manager can update product status.", "error");
            return RedirectToPage(new { View = "Products" });
        }

        Product? product = _dataStore.FindProduct(productID);
        if (product == null)
        {
            SetTempMessage("Product could not be found.", "error");
            return RedirectToPage(new { View = "Products" });
        }

        product.IsActive = isActive;
        _dataStore.SaveProduct(product);
        SetTempMessage($"{product.ProductName} is now {(isActive ? "active" : "inactive")}.", "success");
        return RedirectToPage(new { View = "Products" });
    }

    public IActionResult OnPostAddStaff()
    {
        if (!IsLoggedIn || ActiveUserRole != "Manager")
        {
            SetTempMessage("Only the manager can add staff members.", "error");
            return RedirectToPage(new { View = "Staff" });
        }

        if (string.IsNullOrWhiteSpace(StaffForm.FullName)
            || string.IsNullOrWhiteSpace(StaffForm.Username)
            || string.IsNullOrWhiteSpace(StaffForm.Password))
        {
            SetTempMessage("Full name, username and password are required.", "error");
            return RedirectToPage(new { View = "Staff" });
        }

        if (_dataStore.UsernameExists(StaffForm.Username))
        {
            SetTempMessage("That username is already in use.", "error");
            return RedirectToPage(new { View = "Staff" });
        }

        _dataStore.AddUser(new User
        {
            FullName = StaffForm.FullName,
            Role = StaffForm.Role,
            Username = StaffForm.Username,
            Password = StaffForm.Password
        });

        SetTempMessage($"{StaffForm.FullName} has been added as {StaffForm.Role}.", "success");
        return RedirectToPage(new { View = "Staff" });
    }

    public IActionResult OnPostUpdateStaff(int userID, string fullName, string role, string username, string? password)
    {
        if (!IsLoggedIn || ActiveUserRole != "Manager")
        {
            SetTempMessage("Only the manager can update staff members.", "error");
            return RedirectToPage(new { View = "Staff" });
        }

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username))
        {
            SetTempMessage("Full name and username are required.", "error");
            return RedirectToPage(new { View = "Staff" });
        }

        User? existingUser = _dataStore.FindUser(userID);
        if (existingUser == null)
        {
            SetTempMessage("Staff member could not be found.", "error");
            return RedirectToPage(new { View = "Staff" });
        }

        if (_dataStore.UsernameExists(username, userID))
        {
            SetTempMessage("That username is already in use.", "error");
            return RedirectToPage(new { View = "Staff" });
        }

        existingUser.FullName = fullName;
        existingUser.Role = role;
        existingUser.Username = username;
        existingUser.Password = password ?? string.Empty;
        _dataStore.UpdateUser(existingUser);

        SetTempMessage($"{fullName}'s staff information has been updated.", "success");
        return RedirectToPage(new { View = "Staff" });
    }

    public IActionResult OnPostDeleteStaff(int userID)
    {
        if (!IsLoggedIn || ActiveUserRole != "Manager")
        {
            SetTempMessage("Only the manager can delete staff members.", "error");
            return RedirectToPage(new { View = "Staff" });
        }

        User? existingUser = _dataStore.FindUser(userID);
        if (existingUser == null)
        {
            SetTempMessage("Staff member could not be found.", "error");
            return RedirectToPage(new { View = "Staff" });
        }

        if (existingUser.FullName == ActiveUserName)
        {
            SetTempMessage("You cannot delete the account you are currently using.", "error");
            return RedirectToPage(new { View = "Staff" });
        }

        try
        {
            _dataStore.DeleteUser(userID);
            SetTempMessage($"{existingUser.FullName} has been deleted.", "success");
        }
        catch (MySqlException)
        {
            SetTempMessage("This staff member cannot be deleted because they are linked to existing sales records.", "error");
        }

        return RedirectToPage(new { View = "Staff" });
    }

    public Product? ProductBeingEdited()
    {
        return ProductForm.ProductID == 0 ? null : _dataStore.FindProduct(ProductForm.ProductID);
    }

    public string Money(decimal amount)
    {
        return $"R{amount:N2}";
    }

    public string ActiveViewClass(string viewName)
    {
        return View.Equals(viewName, StringComparison.OrdinalIgnoreCase) ? "active" : string.Empty;
    }

    private List<SaleItem> GetCurrentSaleItems()
    {
        string? json = HttpContext.Session.GetString(SaleItemsSessionKey);
        return string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<SaleItem>>(json) ?? [];
    }

    private void SaveCurrentSaleItems(List<SaleItem> items)
    {
        HttpContext.Session.SetString(SaleItemsSessionKey, JsonSerializer.Serialize(items));
    }

    private void SetTempMessage(string message, string messageType)
    {
        TempData["Message"] = message;
        TempData["MessageType"] = messageType;
    }

    public class LoginInput
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Cashier";
    }

    public class SaleInput
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerContact { get; set; } = string.Empty;
        public int ProductID { get; set; }
        public decimal QuantitySold { get; set; } = 1m;
        public string PaymentMethod { get; set; } = "Cash";
    }

    public class ProductInput
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = "Beef";
        public decimal UnitPrice { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal ReorderLevel { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class StaffInput
    {
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "Cashier";
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
