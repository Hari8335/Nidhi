namespace Nidhi.Domain.Entities;

public enum TransactionType { WALLET_FUNDING, GOLD_PURCHASE }
public enum TransactionStatus { COMPLETED }
public enum LedgerUnit { LKR, GOLD_GRAMS }
public enum AccountClassification { ASSET, LIABILITY, EQUITY, CLEARING }
public enum EntryDirection { DEBIT, CREDIT }
public enum GoalStatus { ACTIVE, COMPLETED, REPLACED }
public enum IdempotencyOperation { SimulateFunding, SaveGold }
public enum IdempotencyStatus { IN_PROGRESS, COMPLETED, FAILED }
