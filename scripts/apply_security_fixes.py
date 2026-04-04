#!/usr/bin/env python3
"""
Auto-applies the two security fixes to the Hali codebase.
Run from the repo root: python3 scripts/apply_security_fixes.py

Fixes applied:
  BLOCKING-2: Remove X-Institution-Id header bypass in OfficialPostsController
  BLOCKING-4: Add rate limiting to POST /v1/signals/preview in SignalsController
"""

import sys, re
from pathlib import Path

ROOT = Path(__file__).parent.parent
SRC  = ROOT / "src"

def find_file(name: str) -> Path | None:
    matches = list(SRC.rglob(name))
    if not matches:
        print(f"  ✗ {name} not found under src/")
        return None
    if len(matches) > 1:
        print(f"  ⚠ Multiple matches for {name}: {matches}")
    return matches[0]

def apply_fix(path: Path, find_pattern: str, replacement: str, label: str) -> bool:
    """Apply a regex substitution. Returns True if changed."""
    text = path.read_text()
    new_text = re.sub(find_pattern, replacement, text, flags=re.DOTALL)
    if new_text == text:
        print(f"  ⚠ {label}: pattern not found — may already be fixed or code differs")
        return False
    path.write_text(new_text)
    print(f"  ✓ {label}: applied")
    return True


# ── BLOCKING-2: OfficialPostsController ────────────────────────────────────────
print("\n── BLOCKING-2: Remove X-Institution-Id header bypass ──")

controller = find_file("OfficialPostsController.cs")
if controller:
    text = controller.read_text()
    
    # Pattern 1: ?? Request.Headers fallback on same or next line
    p1 = r'(var\s+\w*[Ii]nstitution\w*\s*=\s*User\.FindFirstValue\(["\']institution_id["\']\))\s*\?\?\s*Request\.Headers\["X-Institution-Id"\](?:\.ToString\(\)|\.FirstOrDefault\(\))?;'
    r1 = (
        r'\1;\n'
        r'        if (string.IsNullOrEmpty(\2))\n'
        r'            return Forbid();'
    )
    
    # Try to find the var name actually used
    m = re.search(r'var\s+(\w+)\s*=\s*User\.FindFirstValue\(["\']institution_id["\']\)', text)
    var_name = m.group(1) if m else "institutionClaim"
    
    # Pattern: the ?? header fallback
    pattern = (
        r'(var\s+' + re.escape(var_name) + r'\s*=\s*)'
        r'User\.FindFirstValue\(["\']institution_id["\']\)'
        r'\s*\?\?\s*Request\.Headers\["X-Institution-Id"\](?:\.ToString\(\)|\.FirstOrDefault\(\)|\.FirstOrDefault\(\)\s*\?\?\s*string\.Empty)?;'
    )
    
    replacement = (
        f'var {var_name} = User.FindFirstValue("institution_id");\n'
        f'        if (string.IsNullOrEmpty({var_name}))\n'
        f'            return Forbid(); // institution_id must come from JWT — no header fallback'
    )
    
    if re.search(pattern, text, re.DOTALL):
        new_text = re.sub(pattern, replacement, text, flags=re.DOTALL)
        controller.write_text(new_text)
        print(f"  ✓ BLOCKING-2: header bypass removed from {controller.relative_to(ROOT)}")
    elif "X-Institution-Id" in text:
        # Manual guidance
        print(f"  ⚠ Found X-Institution-Id in {controller.relative_to(ROOT)}")
        print(f"     Auto-fix couldn't match the exact pattern.")
        print(f"     Please manually remove the line:")
        for i, line in enumerate(text.split('\n'), 1):
            if 'X-Institution-Id' in line:
                print(f"     Line {i}: {line.strip()}")
        print(f"     Replace the entire institution identity lookup with:")
        print(f"     var {var_name} = User.FindFirstValue(\"institution_id\");")
        print(f"     if (string.IsNullOrEmpty({var_name})) return Forbid();")
    else:
        print(f"  ✓ BLOCKING-2: X-Institution-Id header not found — already fixed or not implemented this way")


# ── BLOCKING-4: SignalsController preview rate limiting ────────────────────────
print("\n── BLOCKING-4: Add rate limiting to POST /v1/signals/preview ──")

signals_ctrl = find_file("SignalsController.cs")
if signals_ctrl:
    text = signals_ctrl.read_text()
    
    # Check if rate limiting already exists
    if "rl:signal-preview" in text or "signal-preview" in text:
        print("  ✓ BLOCKING-4: Rate limiting already present — no change needed")
    elif "Preview" not in text:
        print("  ⚠ BLOCKING-4: Preview action not found in SignalsController.cs")
        print("     Check if it's in a different controller or file.")
    else:
        # Find the preview action and inject rate limiting at the start
        # Strategy: find the method body opening brace and inject after it
        
        # Check if redis is injected via constructor
        has_constructor_redis = "_redis" in text or "IConnectionMultiplexer" in text
        
        rate_limit_block_constructor = '''
        // BLOCKING-4 fix: rate limit anonymous NLP previews (10/IP/10min)
        var _previewIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var _previewKey = $"rl:signal-preview:{_previewIp}";
        var _previewDb = _redis.GetDatabase();
        var _previewCount = await _previewDb.StringIncrementAsync(_previewKey);
        if (_previewCount == 1) await _previewDb.KeyExpireAsync(_previewKey, TimeSpan.FromMinutes(10));
        if (_previewCount > 10) return StatusCode(429, new { code = "rate_limited", message = "Too many preview requests." });
        '''
        
        rate_limit_block_fromservices = '''
        // BLOCKING-4 fix: rate limit anonymous NLP previews (10/IP/10min)
        var _previewIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var _previewKey = $"rl:signal-preview:{_previewIp}";
        var _previewDb = redis.GetDatabase();
        var _previewCount = await _previewDb.StringIncrementAsync(_previewKey);
        if (_previewCount == 1) await _previewDb.KeyExpireAsync(_previewKey, TimeSpan.FromMinutes(10));
        if (_previewCount > 10) return StatusCode(429, new { code = "rate_limited", message = "Too many preview requests." });
        '''
        
        # Find the Preview method and inject after its opening brace
        preview_pattern = r'((?:public|private)\s+async\s+Task<IActionResult>\s+Preview\s*\([^)]*\)\s*\{)'
        
        if re.search(preview_pattern, text):
            if has_constructor_redis:
                inject = rate_limit_block_constructor
            else:
                # Need to add [FromServices] parameter
                # First add the using if needed
                if "using StackExchange.Redis" not in text:
                    text = text.replace("namespace ", "using StackExchange.Redis;\n\nnamespace ", 1)
                
                # Add redis parameter to Preview signature
                text = re.sub(
                    r'((?:public|private)\s+async\s+Task<IActionResult>\s+Preview\s*\()((?:[^)]*))\)',
                    lambda m: (
                        m.group(1) + m.group(2).rstrip() +
                        (", " if m.group(2).strip() else "") +
                        "[FromServices] IConnectionMultiplexer redis)"
                    ),
                    text
                )
                inject = rate_limit_block_fromservices
            
            new_text = re.sub(
                r'((?:public|private)\s+async\s+Task<IActionResult>\s+Preview\s*\([^)]*\)\s*\{)',
                r'\1' + inject,
                text
            )
            signals_ctrl.write_text(new_text)
            print(f"  ✓ BLOCKING-4: Rate limiting injected into Preview action in {signals_ctrl.relative_to(ROOT)}")
        else:
            print(f"  ⚠ Could not find Preview method signature automatically.")
            print(f"     Add this block manually at the start of your Preview action body:")
            print(rate_limit_block_constructor if has_constructor_redis else rate_limit_block_fromservices)


# ── Summary ────────────────────────────────────────────────────────────────────
print("""
── Next steps ──────────────────────────────────────────────────────
1. Review the changes in VS Code:
   code src/Hali.Api/Controllers/OfficialPostsController.cs
   code src/Hali.Api/Controllers/SignalsController.cs

2. Build to verify no compile errors:
   dotnet build src/Hali.Api/

3. Run unit tests:
   dotnet test tests/Hali.Tests.Unit/

4. Commit:
   git add -A
   git commit -m "fix: security — remove institution header bypass, add preview rate limiting"
   git push origin main
────────────────────────────────────────────────────────────────────
""")
