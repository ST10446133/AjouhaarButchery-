<?php
require_once __DIR__ . '/config.php';

try {
    $serverConnection = openDatabaseConnection(false);
    $serverConnection->exec('CREATE DATABASE IF NOT EXISTS ajouhaar_butchery CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci');

    $pdo = openDatabaseConnection(true);

    $schema = file_get_contents(__DIR__ . '/schema.sql');
    $pdo->exec($schema);

    seedUsers($pdo);
    seedProducts($pdo);
    seedCustomers($pdo);
    seedSales($pdo);

    echo '<h1>Ajouhaar Butchery database created successfully.</h1>';
    echo '<p>Database name: <strong>ajouhaar_butchery</strong></p>';
    echo '<p>Default passwords are all <strong>1234</strong>.</p>';
} catch (PDOException $error) {
    http_response_code(500);
    echo '<h1>Database setup failed</h1>';
    echo '<pre>' . htmlspecialchars($error->getMessage()) . '</pre>';
}

function seedUsers(PDO $pdo): void
{
    $users = [
        ['Shuaib Ajouhaar', 'Manager', 'sajouhaar', '1234'],
        ['Counter Staff 1', 'Cashier', 'cashier1', '1234'],
        ['Counter Staff 2', 'Cashier', 'cashier2', '1234'],
    ];

    $statement = $pdo->prepare(
        'INSERT INTO users (full_name, role, username, password_hash)
         VALUES (?, ?, ?, ?)
         ON DUPLICATE KEY UPDATE full_name = VALUES(full_name), role = VALUES(role)'
    );

    foreach ($users as $user) {
        $statement->execute([
            $user[0],
            $user[1],
            $user[2],
            password_hash($user[3], PASSWORD_DEFAULT),
        ]);
    }
}

function seedProducts(PDO $pdo): void
{
    $products = [
        [101, 'Beef Mince', 'Beef', 89.99, 45.5, 10.0, 1, '2026-05-09'],
        [102, 'Lamb Chops', 'Lamb', 159.99, 18.0, 8.0, 1, '2026-05-09'],
        [103, 'Chicken Fillet', 'Poultry', 74.99, 52.0, 12.0, 1, '2026-05-09'],
    ];

    $statement = $pdo->prepare(
        'INSERT INTO products (product_id, product_name, category, unit_price, quantity_on_hand, reorder_level, is_active, last_updated)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?)
         ON DUPLICATE KEY UPDATE
            product_name = VALUES(product_name),
            category = VALUES(category),
            unit_price = VALUES(unit_price),
            quantity_on_hand = VALUES(quantity_on_hand),
            reorder_level = VALUES(reorder_level),
            is_active = VALUES(is_active),
            last_updated = VALUES(last_updated)'
    );

    foreach ($products as $product) {
        $statement->execute($product);
    }
}

function seedCustomers(PDO $pdo): void
{
    $customers = [
        [1, 'Walk-in Customer', 'N/A'],
        [2, 'A. Khan', '0821112345'],
        [3, 'M. Jacobs', '0735558901'],
    ];

    $statement = $pdo->prepare(
        'INSERT INTO customers (customer_id, name, contact_number)
         VALUES (?, ?, ?)
         ON DUPLICATE KEY UPDATE name = VALUES(name), contact_number = VALUES(contact_number)'
    );

    foreach ($customers as $customer) {
        $statement->execute($customer);
    }
}

function seedSales(PDO $pdo): void
{
    $saleCount = (int)$pdo->query('SELECT COUNT(*) FROM sales')->fetchColumn();
    if ($saleCount > 0) {
        return;
    }

    $sales = [
        [301, 'REC-000301', 1, 2, '2026-05-09 09:15:00', 269.97, 'Cash', [[101, 3.0, 89.99, 269.97]]],
        [302, 'REC-000302', 2, 2, '2026-05-09 10:05:00', 319.98, 'Card', [[102, 2.0, 159.99, 319.98]]],
        [303, 'REC-000303', 3, 3, '2026-05-09 12:20:00', 149.98, 'Cash', [[103, 2.0, 74.99, 149.98]]],
    ];

    $saleStatement = $pdo->prepare(
        'INSERT INTO sales (sale_id, receipt_number, customer_id, user_id, sale_date, total_amount, payment_method)
         VALUES (?, ?, ?, ?, ?, ?, ?)'
    );
    $itemStatement = $pdo->prepare(
        'INSERT INTO sale_items (sale_id, product_id, quantity_sold, unit_price, line_total)
         VALUES (?, ?, ?, ?, ?)'
    );
    $receiptStatement = $pdo->prepare(
        'INSERT INTO receipts (sale_id, receipt_number, printed_date)
         VALUES (?, ?, ?)'
    );

    foreach ($sales as $sale) {
        $saleStatement->execute(array_slice($sale, 0, 7));
        foreach ($sale[7] as $item) {
            $itemStatement->execute([$sale[0], $item[0], $item[1], $item[2], $item[3]]);
        }
        $receiptStatement->execute([$sale[0], $sale[1], $sale[4]]);
    }
}
?>
