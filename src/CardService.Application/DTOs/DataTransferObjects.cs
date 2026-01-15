namespace CardService.Application.DTOs;

/// <summary>
/// Request DTO for creating a new card.
/// </summary>
/// <param name="CardNumber">The 16-digit card number as a string (e.g., "4111111111111111").</param>
/// <param name="CreditLimitUsd">The credit limit in USD (e.g., 5000.00).</param>
public record CreateCardRequest(string CardNumber, decimal CreditLimitUsd);

/// <summary>
/// Response DTO for a successfully created card.
/// </summary>
/// <param name="CardId">The unique identifier of the created card.</param>
public record CreateCardResponse(Guid CardId);

/// <summary>
/// Request DTO for creating a purchase transaction on a card.
/// </summary>
/// <param name="Description">The purchase description (max 50 characters, e.g., "Groceries").</param>
/// <param name="TransactionDate">The date on which the purchase occurred.</param>
/// <param name="AmountUsd">The purchase amount in USD (e.g., 50.00).</param>
public record CreatePurchaseRequest(string Description, DateOnly TransactionDate, decimal AmountUsd);

/// <summary>
/// Response DTO for a successfully created purchase transaction.
/// </summary>
/// <param name="PurchaseId">The unique identifier of the created purchase.</param>
public record CreatePurchaseResponse(Guid PurchaseId);

/// <summary>
/// Response DTO for a purchase transaction with FX conversion applied.
/// </summary>
/// <param name="PurchaseId">The unique identifier of the purchase.</param>
/// <param name="Description">The purchase description.</param>
/// <param name="TransactionDate">The date on which the purchase was made.</param>
/// <param name="AmountUsd">The original purchase amount in USD.</param>
/// <param name="CurrencyKey">The target currency for conversion (Treasury currency key).</param>
/// <param name="ExchangeRate">The exchange rate used for conversion.</param>
/// <param name="RateDate">The date on which the exchange rate is effective.</param>
/// <param name="ConvertedAmount">The purchase amount converted to the target currency, rounded to 2 decimals.</param>
public record ConvertedPurchaseResponse(
    Guid PurchaseId,
    string Description,
    DateOnly TransactionDate,
    decimal AmountUsd,
    string CurrencyKey,
    decimal ExchangeRate,
    DateOnly RateDate,
    decimal ConvertedAmount
);

/// <summary>
/// Response DTO for a card's available balance (credit limit minus purchases).
/// </summary>
/// <remarks>
/// <para>If no <paramref name="CurrencyKey"/> is provided, only the USD balance fields are populated.</para>
/// <para>If a currency key is provided, the optional FX fields contain the converted balance.</para>
/// </remarks>
/// <param name="CardId">The unique identifier of the card.</param>
/// <param name="CreditLimitUsd">The card's credit limit in USD.</param>
/// <param name="TotalPurchasesUsd">The sum of all purchase amounts in USD.</param>
/// <param name="AvailableBalanceUsd">The available balance in USD (credit limit minus purchases).</param>
/// <param name="CurrencyKey">The currency to which the balance was converted, if requested; otherwise null.</param>
/// <param name="ExchangeRate">The exchange rate used for conversion, if a currency was requested; otherwise null.</param>
/// <param name="RateDate">The date on which the exchange rate is effective, if a currency was requested; otherwise null.</param>
/// <param name="ConvertedAvailableBalance">The available balance converted to the target currency, if a currency was requested; otherwise null.</param>
/// <param name="AsOfDate">The anchor date used for FX rate selection, if a currency was requested; otherwise null.</param>
public record BalanceResponse(
    Guid CardId,
    decimal CreditLimitUsd,
    decimal TotalPurchasesUsd,
    decimal AvailableBalanceUsd,
    string? CurrencyKey = null,
    decimal? ExchangeRate = null,
    DateOnly? RateDate = null,
    decimal? ConvertedAvailableBalance = null,
    DateOnly? AsOfDate = null
);
