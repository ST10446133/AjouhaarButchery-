<?php
// Update these values if your MySQL/XAMPP settings are different.
$dbHost = 'localhost';
$dbName = 'ajouhaar_butchery';
$dbUser = 'root';
$dbPassword = '';

function openDatabaseConnection(bool $includeDatabase = true): PDO
{
    global $dbHost, $dbName, $dbUser, $dbPassword;

    $databasePart = $includeDatabase ? ";dbname=$dbName" : '';
    $dsn = "mysql:host=$dbHost$databasePart;charset=utf8mb4";

    return new PDO($dsn, $dbUser, $dbPassword, [
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
        PDO::ATTR_EMULATE_PREPARES => false,
    ]);
}
?>
