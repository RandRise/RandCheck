# Delivery phases

Each phase has one primary outcome. Detailed tasks can change, but the exit criteria protect the product from becoming a collection of disconnected features.

## Sequence

1. [Product definition](./phase-0-product-definition)
2. [Invoice foundation](./phase-1-invoice-foundation)
3. [Real document processing](./phase-2-document-processing)
4. [Review and evaluation](./phase-3-review-evaluation)
5. [Document reconciliation](./phase-4-reconciliation)
6. [Cited document questions](./phase-5-cited-questions)
7. [Enterprise security](./phase-6-enterprise-security)
8. [Arabic support](./phase-7-arabic-support)
9. [Operations and deployment](./phase-8-operations-deployment)

## Dependency flow

```text
Product definition
       ↓
Invoice foundation → Real document processing → Review and evaluation
                                                ↓
                         Reconciliation → Cited questions
                                                ↓
                         Enterprise security → Arabic support
                                                ↓
                                  Operations and deployment
```

Security considerations begin in Phase 1 even though the complete enterprise-control milestone is Phase 6. Similarly, measurement hooks begin early, while formal evaluation is consolidated in Phase 3.

## Review checklist

For each phase, confirm:

- Is the user outcome clear?
- Are included and excluded features explicit?
- Can acceptance criteria be objectively verified?
- Is the required data safe and legally usable?
- What portfolio evidence will the phase create?
- What must be decided before coding begins?
