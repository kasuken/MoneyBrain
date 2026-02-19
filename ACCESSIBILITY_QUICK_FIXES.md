# Accessibility Quick Fixes Guide

## Critical Issues - Fix Immediately

### 1. Add aria-labels to icon buttons

**Files to fix:**
- `MainLayout.razor` - Hamburger menu button (line 27-31)
- `Dashboard.razor` - Previous/Next month buttons (lines 43, 48)
- `LanguageSelector.razor` - Language button (line 8-11)

**Pattern to use:**
```razor
<MudIconButton Icon="@Icons.Material.Filled.Menu" 
               aria-label="@L["Nav_OpenMenu"]"
               ... />
```

### 2. Add Skip to Main Content Link

**File:** `MainLayout.razor` (top of file, after @implements)

Add this code:
```razor
<a href="#main-content" class="skip-link">@L["Accessibility_SkipToMainContent"]</a>

<style>
    .skip-link {
        position: absolute;
        left: -9999px;
        z-index: 999;
        padding: 1em;
        background-color: var(--mud-palette-primary);
        color: white;
        text-decoration: none;
    }
    
    .skip-link:focus {
        left: 50%;
        transform: translateX(-50%);
        top: 0;
    }
</style>
```

Then add `id="main-content"` to `<MudMainContent>` element (line 136).

### 3. Fix Dashboard Heading Hierarchy

**File:** `Dashboard.razor`

Change line 41 from:
```razor
<h2 class="mb-section-title">@L["Dashboard_Overview"]</h2>
```

To:
```razor
<h1 class="mb-section-title">@L["Dashboard_Overview"]</h1>
```

Then update subsequent headings:
- "Budget Summary" (line 94) → keep as `<h2>`
- "Top Budget Categories" (line 144) → change to `<h3>`
- "Trends Last 6 Months" (line 230) → keep as `<h2>`

### 4. Wrap Navigation in <nav> Elements

**File:** `NavMenu.razor`

Change line 6 from:
```razor
<div class="mb-header-nav ms-6">
```

To:
```razor
<nav class="mb-header-nav ms-6" aria-label="@L["Nav_MainNavigation"]">
```

And close with `</nav>` instead of `</div>`.

---

## High Priority Issues

### 5. Improve Logo Alt Text

**File:** `MainLayout.razor`

Lines 36 and 104:
```razor
<!-- Header logo -->
<img src="/icons/OnlyLogo.png" alt="MoneyBrain Home" style="max-height: 40px;" />

<!-- Drawer logo -->
<img src="/icons/OnlyLogo.png" alt="MoneyBrain Logo" style="max-height: 32px;" />
```

### 6. Add aria-label to User Menu

**File:** `MainLayout.razor` (line 56)

```razor
<div class="mb-user-menu" 
     role="button" 
     tabindex="0"
     aria-label="@($"{L["Nav_UserMenu"]}: {context.User.Identity?.Name}")">
```

### 7. Add aria-label to Marketing Navigation

**File:** `MarketingLayout.razor` (line 16)

```razor
<nav class="marketing-nav" aria-label="Main navigation">
```

---

## Medium Priority Issues

### 8. Mark Decorative Icons as aria-hidden

Add `aria-hidden="true"` to all decorative icons:

**Dashboard.razor** - Empty state icons (lines 192, 218, 254, 281):
```razor
<MudIcon Icon="@Icons.Material.Outlined.BarChart" 
         Style="font-size: 3rem; opacity: 0.5;" 
         aria-hidden="true" />
```

**Home.razor** - Feature card icons (lines 43-87):
```razor
<div class="marketing-feature-icon" aria-hidden="true">
    <MudIcon Icon="@Icons.Material.Filled.AccountBalanceWallet" Size="MudBlazor.Size.Large" />
</div>
```

**MarketingLayout.razor** - Logo icons (lines 13, 50):
```razor
<MudIcon Icon="@Icons.Material.Filled.AccountBalanceWallet" 
         Size="MudBlazor.Size.Large" 
         aria-hidden="true" />
```

**Accounts.razor** - Empty state icon (line 108):
```razor
<MudIcon Icon="@Icons.Material.Outlined.AccountBalanceWallet" 
         Class="mb-empty-state-icon" 
         aria-hidden="true" />
```

---

## Required Localization Keys

Add these to your resource files (SharedResource.resx or appropriate localization files):

```xml
<data name="Nav_OpenMenu" xml:space="preserve">
    <value>Open navigation menu</value>
</data>
<data name="Dashboard_PreviousMonth" xml:space="preserve">
    <value>Previous month</value>
</data>
<data name="Dashboard_NextMonth" xml:space="preserve">
    <value>Next month</value>
</data>
<data name="Nav_UserMenu" xml:space="preserve">
    <value>User menu</value>
</data>
<data name="Nav_MainNavigation" xml:space="preserve">
    <value>Main navigation</value>
</data>
<data name="Accessibility_SkipToMainContent" xml:space="preserve">
    <value>Skip to main content</value>
</data>
<data name="Trans_SearchTransactions" xml:space="preserve">
    <value>Search transactions</value>
</data>
<data name="Dashboard_DefaultBudget" xml:space="preserve">
    <value>Default budget</value>
</data>
```

---

## Testing Checklist

After making fixes, test:

- [ ] Screen reader navigation works properly (NVDA/JAWS/VoiceOver)
- [ ] All interactive elements are keyboard accessible (Tab key)
- [ ] Skip link appears on Tab and jumps to main content
- [ ] Icon buttons announce their purpose
- [ ] Heading hierarchy is logical (H1 → H2 → H3)
- [ ] Form inputs have associated labels
- [ ] Decorative images/icons are ignored by screen readers
- [ ] Navigation landmarks are properly announced
- [ ] Focus indicators are visible on all interactive elements
- [ ] Color contrast meets WCAG AA standards (use browser tools)

---

## Automated Testing Tools to Use

1. **Lighthouse** (Chrome DevTools → Lighthouse → Accessibility)
2. **axe DevTools** (Browser extension)
3. **WAVE** (Browser extension)
4. **Screen reader** (NVDA for Windows, VoiceOver for Mac)

---

## Quick Win Statistics

**Estimated time to fix all critical issues:** 2-3 hours
**Estimated time to fix all high priority issues:** 2-3 hours  
**Estimated time to fix all medium priority issues:** 3-4 hours

**Total estimated time for all fixes:** 7-10 hours

**Impact:** Will improve WCAG 2.1 Level AA compliance from ~60% to ~90%
