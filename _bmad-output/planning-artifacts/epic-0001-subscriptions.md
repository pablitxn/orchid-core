---
epicId: '0001'
title: 'Subscriptions'
phase: 2
priority: P1
masterPrd: prd.md
status: draft
estimatedEffort: '3 weeks'
dependencies:
  - epic-0000
---

# Epic PRD: 0001 - Subscriptions

**Parent:** [Master PRD](./prd.md)
**Phase:** 2 - Complete
**Priority:** P1 (High)
**Author:** Pablo
**Date:** 2026-01-04

---

## Overview

Complete the half-implemented subscription system by finishing Stripe integration, implementing the credit-based usage model, and adding billing management features. The current system has partial implementation that needs to be finished and hardened for production use.

### Current State

Based on codebase analysis:
- Subscription entities exist in domain
- Partial Stripe adapter implementation
- Credit tracking partially implemented
- Missing webhook handlers for billing events
- Incomplete user-facing billing management

### Target State

- Full Stripe subscription lifecycle management
- Robust credit system with usage tracking
- Webhook handlers for all billing events
- User self-service billing portal
- Admin billing oversight capabilities

---

## Goals

| Goal | Success Metric |
|------|----------------|
| Complete Stripe integration | All subscription operations work |
| Reliable webhook processing | 99.9% webhook success rate |
| Credit system operational | Users can purchase and use credits |
| Self-service billing | Users manage subscriptions without support |
| Billing visibility | Admins can view all billing activity |

---

## Scope

### In Scope

1. **Stripe Integration Completion**
   - Subscription creation flow
   - Plan upgrades/downgrades
   - Subscription cancellation
   - Payment method management
   - Invoice handling

2. **Webhook Implementation**
   - `checkout.session.completed`
   - `customer.subscription.created`
   - `customer.subscription.updated`
   - `customer.subscription.deleted`
   - `invoice.paid`
   - `invoice.payment_failed`
   - `payment_intent.succeeded`
   - `payment_intent.payment_failed`

3. **Credit System**
   - Credit purchase flows
   - Credit balance tracking
   - Credit consumption on AI operations
   - Low credit warnings
   - Credit expiration handling

4. **Billing Management API**
   - Get current subscription
   - Get billing history
   - Get/update payment methods
   - Get credit balance
   - Purchase credits
   - Cancel subscription

5. **Admin Features**
   - View all subscriptions
   - Manual credit adjustments
   - Billing issue resolution tools

### Out of Scope

- Frontend billing UI (separate frontend PRD)
- New pricing tiers (use existing plans)
- Custom enterprise billing
- Multi-currency support
- Tax calculation changes

---

## Requirements

### REQ-0001-01: Stripe Subscription Lifecycle

**Description:** Implement complete subscription management through Stripe.

**Acceptance Criteria:**
- [ ] Users can subscribe to any available plan
- [ ] Plan changes take effect according to Stripe proration settings
- [ ] Cancellation schedules subscription end at period end
- [ ] Reactivation possible before period ends
- [ ] Failed payments trigger appropriate user notifications

**Technical Notes:**
- Use Stripe Customer Portal for payment method management
- Store subscription status in local DB, sync via webhooks
- Implement idempotency keys for all Stripe API calls

**API Endpoints:**
```
POST   /api/subscriptions/checkout-session
POST   /api/subscriptions/change-plan
POST   /api/subscriptions/cancel
POST   /api/subscriptions/reactivate
GET    /api/subscriptions/current
GET    /api/subscriptions/portal-session
```

---

### REQ-0001-02: Webhook Processing

**Description:** Reliable handling of all Stripe webhook events.

**Acceptance Criteria:**
- [ ] All listed webhooks handled with appropriate business logic
- [ ] Webhook signature verification enforced
- [ ] Idempotent processing (duplicate events handled gracefully)
- [ ] Failed webhook processing retried with backoff
- [ ] Dead letter queue for persistent failures
- [ ] Webhook processing logged for debugging

**Technical Notes:**
- Use Stripe's webhook signature verification
- Store processed event IDs to prevent duplicates
- Publish domain events for downstream processing

**Webhook Endpoint:**
```
POST   /api/webhooks/stripe
```

---

### REQ-0001-03: Credit System

**Description:** Implement usage-based credit system for AI operations.

**Acceptance Criteria:**
- [ ] Users can purchase credit packages
- [ ] Credit balance decremented on AI API usage
- [ ] Insufficient credits blocks AI operations (graceful error)
- [ ] Low credit threshold triggers notifications
- [ ] Credit transactions logged for audit
- [ ] Credits optionally expire (configurable)

**Technical Notes:**
- Atomic credit deduction (prevent race conditions)
- Use database transactions for credit operations
- Consider Redis for real-time balance checks

**API Endpoints:**
```
GET    /api/credits/balance
POST   /api/credits/purchase
GET    /api/credits/transactions
GET    /api/credits/usage
```

**Domain Events:**
- `CreditsPurchased`
- `CreditsConsumed`
- `LowCreditBalance`
- `CreditsExpired`

---

### REQ-0001-04: Billing Management API

**Description:** Self-service billing management for users.

**Acceptance Criteria:**
- [ ] Users can view current subscription details
- [ ] Users can view billing history (invoices)
- [ ] Users can access Stripe portal for payment methods
- [ ] Users can view credit balance and history
- [ ] Proper authorization (users see only their data)

**API Endpoints:**
```
GET    /api/billing/subscription
GET    /api/billing/invoices
GET    /api/billing/payment-methods
GET    /api/billing/summary
```

---

### REQ-0001-05: Admin Billing Tools

**Description:** Administrative tools for billing management.

**Acceptance Criteria:**
- [ ] Admins can view any user's subscription
- [ ] Admins can manually adjust credits (with reason)
- [ ] Admins can view webhook processing logs
- [ ] Admins can retry failed webhooks
- [ ] All admin actions logged for audit

**API Endpoints:**
```
GET    /api/admin/billing/subscriptions
GET    /api/admin/billing/subscriptions/{userId}
POST   /api/admin/billing/credits/adjust
GET    /api/admin/billing/webhooks/logs
POST   /api/admin/billing/webhooks/{id}/retry
```

---

## Technical Design Considerations

### Data Model Updates

```
Subscription
├── Id (Guid)
├── UserId (Guid)
├── StripeCustomerId (string)
├── StripeSubscriptionId (string)
├── PlanId (string)
├── Status (enum: Active, Canceled, PastDue, Trialing)
├── CurrentPeriodStart (DateTime)
├── CurrentPeriodEnd (DateTime)
├── CancelAtPeriodEnd (bool)
└── Timestamps

CreditBalance
├── Id (Guid)
├── UserId (Guid)
├── Balance (decimal)
├── LastUpdated (DateTime)
└── Version (int) -- optimistic concurrency

CreditTransaction
├── Id (Guid)
├── UserId (Guid)
├── Type (enum: Purchase, Consumption, Adjustment, Expiration)
├── Amount (decimal)
├── BalanceAfter (decimal)
├── Reference (string) -- e.g., Stripe payment ID, AI operation ID
├── Reason (string) -- for adjustments
├── CreatedAt (DateTime)
└── CreatedBy (Guid?) -- for admin adjustments

WebhookEvent
├── Id (Guid)
├── StripeEventId (string) -- for idempotency
├── Type (string)
├── Payload (jsonb)
├── ProcessedAt (DateTime?)
├── Status (enum: Pending, Processed, Failed)
├── ErrorMessage (string?)
├── RetryCount (int)
└── CreatedAt (DateTime)
```

### Architecture

```
WebApi
└── Controllers/
    ├── SubscriptionController.cs
    ├── CreditController.cs
    ├── BillingController.cs
    └── WebhookController.cs

Application
└── UseCases/
    ├── Subscriptions/
    │   ├── CreateCheckoutSession/
    │   ├── ChangePlan/
    │   ├── CancelSubscription/
    │   └── Queries/
    ├── Credits/
    │   ├── PurchaseCredits/
    │   ├── ConsumeCredits/
    │   ├── AdjustCredits/
    │   └── Queries/
    └── Webhooks/
        └── ProcessStripeWebhook/

Infrastructure
└── Adapters/
    └── Stripe/
        ├── StripeSubscriptionService.cs
        ├── StripeCreditService.cs
        └── StripeWebhookHandler.cs
```

### Error Handling

| Scenario | Response |
|----------|----------|
| Invalid Stripe API call | Log, return 500 with generic message |
| Webhook signature invalid | Return 400, do not process |
| Insufficient credits | Return 402 Payment Required |
| Subscription not found | Return 404 |
| Duplicate webhook | Return 200 (idempotent), don't reprocess |

---

## Dependencies

### External
- Stripe API (subscription management, webhooks)
- Stripe Customer Portal (payment method UI)

### Internal
- Epic 0000 must be complete (stable codebase)
- User authentication system (for user association)

### Blocked By This
- Epic 0003 (Agent Framework) - needs credit system for usage metering
- Epic 0004 (Marketplace) - needs subscription tiers for access control

---

## Migration Strategy

1. **Phase 1:** Complete backend implementation with feature flags
2. **Phase 2:** Enable for test users, validate webhook flow
3. **Phase 3:** Migrate existing partial data to new schema
4. **Phase 4:** Enable for all users
5. **Phase 5:** Remove old subscription code

---

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Subscription operations | 99.9% success | Stripe dashboard + logs |
| Webhook processing | 99.9% success | Webhook event table |
| Credit operations | 100% accuracy | Balance reconciliation |
| User self-service | 90% billing issues | Support ticket reduction |

---

## Open Questions

1. What are the exact credit costs per AI operation type?
2. Should credits expire? If so, what's the policy?
3. What's the grace period for failed payments before suspension?
4. Should free tier users have limited credits or no access to paid features?
5. How to handle refunds - restore credits or just Stripe refund?

---

*This is an Epic PRD. See [Master PRD](./prd.md) for overall project context.*
