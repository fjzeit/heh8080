# CPU Test Integration

heh8080 supports standard CPU test suites for validating both Intel 8080 and Zilog Z80 emulator accuracy.

## 8080 Test Suites

| File | Purpose |
|------|---------|
| TST8080.COM | Basic CPU diagnostic |
| 8080PRE.COM | Preliminary validation |
| CPUTEST.COM | General CPU testing |
| 8080EXM.COM | Comprehensive exerciser (hours to run) |

## Z80 Test Suites

| File | Purpose |
|------|---------|
| ZEXDOC.COM | Tests documented flag behavior only |
| ZEXALL.COM | Tests all flags including undocumented (X/Y) |

**Note**: ZEXALL/ZEXDOC run 68 tests, each taking 3-4 minutes. Full run takes ~4.5 hours.

## Configuration

Set `HEH8080_CPU_TESTS` to your test files directory:

```bash
export HEH8080_CPU_TESTS=/path/to/cpu_tests
```

If not set, defaults to `tests/cpu_tests/`.

## Sources

### 8080 Tests
- https://altairclone.com/downloads/cpu_tests/
- https://github.com/superzazu/8080/tree/master/cpu_tests

### Z80 Tests
- https://github.com/agn453/ZEXALL (Frank D. Cringle's exerciser)
- https://mdfs.net/Software/Z80/Exerciser/
- https://github.com/begoon/z80exer
