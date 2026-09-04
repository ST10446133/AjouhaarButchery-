<?php
require_once __DIR__ . '/config.php';

function validateLogin(string $username, string $password, string $role): ?array
{
    $pdo = openDatabaseConnection(true);
    $statement = $pdo->prepare(
        'SELECT user_id, full_name, role, username, password_hash
         FROM users
         WHERE username = ? AND role = ?
         LIMIT 1'
    );
    $statement->execute([$username, $role]);
    $user = $statement->fetch();

    if (!$user || !password_verify($password, $user['password_hash'])) {
        return null;
    }

    unset($user['password_hash']);
    return $user;
}
?>
