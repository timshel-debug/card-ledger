Below is the document text, cleaned up and formatted (headings, numbering, and bullet lists preserved). 

---

## Summary

Your task is to build an application that supports the requirements outlined below. Outside of the requirements outlined, as well as any language limitations specified by the technical implementation notes below and/or by the hiring manager(s), the application is your own design from a technical perspective.

This is your opportunity to show us what you know. Have fun, explore new ideas, and as noted in the Questions section below, please let us know if you have any questions regarding the requirements.

---

## Requirements

### Requirement #1: Create a Card

Your application must be able to accept and store (i.e., persist) a Card with a credit limit in United States dollars. When the card is stored, it will be assigned a unique identifier. A card can have zero or more purchase transactions associated with it (see Requirement #2 below).

**Field requirements:**

* **Unique identifier:** must uniquely identify the card
* **Card Number:** must be a 16-digit numeric string that uniquely identifies the card (e.g., the number that would appear on a credit or debit card)
* **Credit limit:** must be a valid positive amount rounded to the nearest cent

---

### Requirement #2: Store a Purchase Transaction

Your application must be able to accept and store (i.e., persist) a Purchase Transaction associated with a specific card. A purchase transaction includes a description, transaction date, and a purchase amount in United States dollars. When the transaction is stored, it will be assigned a unique identifier.

**Field requirements:**

* **Unique identifier:** must uniquely identify the purchase
* **Description:** must not exceed 50 characters
* **Transaction date:** must be a valid date format
* **Purchase amount:** must be a valid positive amount rounded to the nearest cent

---

### Requirement #3: Retrieve a Purchase Transaction in a specified currency

Based upon purchase transactions previously submitted and stored, your application must provide a way to retrieve stored purchase transactions converted to currencies supported by the Treasury Reporting Rates of Exchange API, based upon the exchange rate active for the date of the purchase.

Treasury dataset:

* [https://fiscaldata.treasury.gov/datasets/treasury-reporting-rates-exchange/treasury-reporting-rates-of-exchange](https://fiscaldata.treasury.gov/datasets/treasury-reporting-rates-exchange/treasury-reporting-rates-of-exchange)

The retrieved purchase should include:

* the identifier
* the description
* the transaction date
* the original US dollar purchase amount
* the exchange rate used
* the converted amount based upon the specified currency’s exchange rate for the date of the purchase

**Currency conversion requirements:**

* When converting between currencies, you do not need an exact date match, but must use a currency conversion rate **less than or equal to the purchase date** from within the **last 6 months**.
* If no currency conversion rate is available within 6 months equal to or before the purchase date, an error should be returned stating the purchase cannot be converted to the target currency.
* The converted purchase amount to the target currency should be rounded to **two decimal places** (i.e., cent).

---

### Requirement #4: Retrieve the Available Balance of a Card in a specified currency

Provide a way to retrieve a card’s available balance.

The available balance is calculated as the card’s credit limit minus the total of all purchase transactions recorded for that card.

**Currency conversion requirements:**

* When converting to another currency, apply the same rules described in Requirement #3 (Currency conversion requirements).

---

## Technical Implementation

The technical implementation, including frameworks, libraries, etc. is your own design except for the language. That is, if you are applying for:

* (written in C#):** you should implement the solution in C#.

You should build this application as if you are building an application to be deployed in a Production environment. This should be interpreted to mean that all functional automated testing you would include for a Production application should be expected.

Please note that non-functional test (e.g., performance testing) automation is not needed.

Your application repository should be fully functional without installing separate databases, web servers, or servlet containers (e.g., Jetty, Tomcat, etc.).

---
