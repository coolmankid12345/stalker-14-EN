ent-PrizeTicket = caravan voucher
   .desc = A voucher used for exchange through a special "trade terminal". Lets you get fairly powerful weapons, if you have enough vouchers.
ent-PrizeTicket1 = { ent-PrizeTicket }
   .suffix = 1
   .desc = { ent-PrizeTicket.desc }
ent-PrizeTicket10  = { ent-PrizeTicket }
   .suffix = 10
   .desc = { ent-PrizeTicket.desc }
ent-PrizeTicket30  = { ent-PrizeTicket }
   .suffix = 30
   .desc = { ent-PrizeTicket.desc }
ent-PrizeTicket60  = { ent-PrizeTicket }
   .suffix = 60
   .desc = { ent-PrizeTicket.desc }
nc-store-window-title = Trade Terminal
nc-store-select-category = Select a category
nc-store-search-placeholder = Search items...
nc-store-footer-balance = Balance:
nc-store-tab-buy = Buy
nc-store-tab-sell = Sell
nc-store-tab-contracts = Contracts
nc-store-cat-ready-short = Ready
nc-store-cat-crate-short = In crate
nc-store-cat-ready-full = Ready for sale
nc-store-cat-crate-full = Ready for sale (in crate)
nc-store-category-fallback = Misc
nc-store-mass-sell-button = Sell crate contents
nc-store-mass-sell-tooltip = Option for quickly selling all crate contents.
    Conditions:
    - Crate must be closed
    - You must drag the crate behind you
nc-store-mass-sell-tooltip-with-reward = { nc-store-mass-sell-tooltip }

    Estimated value: { $reward }
nc-store-only-mass-sell = This item can only be sold in bulk through a closed crate.
nc-store-show-more = Show more ({ $count })
nc-store-prompt-select-category = Please select a category on the left.
nc-store-empty-search = No items found for your query.
nc-store-empty-category-search = No matching items in this category.
nc-store-search-results-buy = Search results (Buy): { $count }
nc-store-search-results-sell = Search results (Sell): { $count }
nc-store-no-stock = Out of stock
nc-store-buying-finished = Limit reached
nc-store-remaining = Remaining: { $count }
nc-store-will-buy = Required: { $count }
nc-store-owned = You have: { $count }
nc-store-no-access = Access error
nc-store-contracts-empty = No active contracts right now. Check again later.
nc-store-slot-cooldowns-header = Order refresh
nc-store-slot-cooldown-title = { $difficulty }
nc-store-difficulty-easy = Easy
nc-store-difficulty-medium = Medium
nc-store-difficulty-hard = Hard
nc-store-contract-title = Contract ({ $difficulty })
nc-store-contract-badge-single = One-time
nc-store-contract-badge-single-tooltip =
    This contract can only be completed once per shift.
    After completion, it disappears from the list.
nc-store-contract-goals-header = Contract goals:
nc-store-contract-turn-in-header = Turn in at terminal:
nc-store-contract-turn-in-note = After completion: { $item }
nc-store-contract-reward-header = Reward:
nc-store-contract-items-header = Items:
nc-store-contract-action-claim = Complete contract
nc-store-contract-action-claim-progress = Turn in part ({ $progress }/{ $required })
nc-store-contract-action-can-claim = Ready to turn in
nc-store-contract-action-can-claim-proof = Ready, proof required
nc-store-contract-action-not-taken = Not taken
nc-store-contract-action-not-done = Not completed
nc-store-contract-action-take = Take contract
nc-store-contract-claim-tooltip-single = Complete this one-time contract and receive full reward.
nc-store-contract-claim-tooltip-repeatable = Turn in current contract progress and receive reward.
nc-store-contract-claim-tooltip-not-done = Contract conditions are not met yet. Not enough items.
nc-store-contract-take-tooltip = Accept this contract. Once accepted, it cannot be skipped.
nc-store-contract-completed = Contract completed successfully!
nc-store-contract-taken = Contract accepted.
nc-store-contract-take-failed = Failed to accept contract.
nc-store-contract-goal-line = { $item }: { $count } pcs.
nc-store-contract-progress-line = Progress: { $progress } of { $required }
nc-store-contract-progress-caption = Progress
nc-store-contract-progress-value = { $progress } / { $required }
nc-store-currency-format = { $amount } { $currency }
nc-store-contract-title-pretty = Contract: { $difficulty } - { $goal }
nc-store-contract-title-pretty-nogoal = Contract: { $difficulty }

nc-store-contract-desc-default = Fulfill the contract requirements and claim your reward.
nc-store-contract-desc-generated = Required: { $goals }

nc-store-contract-goal-inline = { $item } x{ $count }

nc-store-unknown-item = ???

nc-store-proto-tooltip-name-only = { $name }
nc-store-proto-tooltip = { $name }
    { $desc }

nc-store-contract-reward-none = Reward not specified
nc-store-contract-reward-item-line = { $item } x{ $count }

nc-store-contract-badge-taken = IN PROGRESS
nc-store-contract-badge-taken-tooltip = This contract is already in your hands. It can no longer be removed from the board.
nc-store-contract-badge-completed = DONE
nc-store-contract-badge-completed-tooltip = Job finished. Turn in the result and collect payment.

nc-store-contract-action-skip = Replace ({ $cost } { $currency })
nc-store-contract-skip-tooltip =
    Remove this contract from the board and replace it with a new one.
    Cost: { $cost } { $currency }.
nc-store-contract-skipped = Contract removed. A new one appeared in its place.
nc-store-contract-skip-failed = Failed to replace contract. Not enough funds.
nc-store-contract-skip-locked = This contract is already in your hands. It cannot be removed.


nc-store-contract-type-delivery = Delivery
nc-store-contract-type-delivery-tooltip = Standard order to deliver the required goods.

nc-store-contract-type-hunt = Bounty Hunt
nc-store-contract-type-hunt-tooltip = After acceptance, a target will appear. Eliminate it, take proof, and return for payment.

nc-store-contract-type-repair = Repair
nc-store-contract-type-repair-tooltip = Multi-stage order to restore an object. After repairs, bring proof of completion.

nc-store-contract-type-ghost-role = Special target
nc-store-contract-type-ghost-role-tooltip = After acceptance, a special role opens. If someone takes it, a live target appears.

nc-store-contract-runtime-stage = Stage: { $stage } of { $goal }

nc-store-contract-action-pinpointer = Issue pinpointer
nc-store-contract-action-pinpointer-tooltip = Issue a new pinpointer for the current active contract target.
nc-store-contract-pinpointer-issued = Pinpointer issued.
nc-store-contract-pinpointer-issue-failed = Failed to issue pinpointer.
nc-store-contract-ghost-role-timeout = Nobody took this role in time. Contract failed.
nc-store-contract-ghost-role-target-lost = Target was lost before the operation began. Contract failed.

nc-store-contract-badge-awaiting-ghost-role = WAITING
nc-store-contract-badge-awaiting-ghost-role-tooltip = Looking for an assignee. If no one accepts in time, the contract fails.
nc-store-contract-badge-ghost-role-active = TARGET ACTIVE
nc-store-contract-badge-ghost-role-active-tooltip = Target is active. Eliminate it and deliver the body to the trade terminal.
nc-store-contract-ghost-role-waiting-line = Looking for assignee: { $time }
nc-store-contract-ghost-role-active-line = Target is in the field. Eliminate it and deliver the body to the trade terminal.


nc-store-contract-delivery-target-lost = Cargo lost. Contract failed.
nc-store-contract-proof-generation-failed = Completion proof was not generated. Contract failed.
nc-store-contract-proof-lost = Proof destroyed. Contract failed.
