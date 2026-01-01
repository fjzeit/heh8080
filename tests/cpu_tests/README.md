# 8080 CPU Test Integration

heh8080 supports standard Intel 8080 CPU test suites for validating emulator accuracy.

## Supported Test Suites

| File | Purpose |
|------|---------|
| TST8080.COM | Basic CPU diagnostic |
| 8080PRE.COM | Preliminary validation |
| CPUTEST.COM | General CPU testing |
| 8080EXM.COM | Comprehensive exerciser |

## Configuration

Set `HEH8080_CPU_TESTS` to your test files directory:

```bash
export HEH8080_CPU_TESTS=/path/to/cpu_tests
```

If not set, defaults to `tests/cpu_tests/`.

## Sources

- https://altairclone.com/downloads/cpu_tests/
- https://github.com/superzazu/8080/tree/master/cpu_tests
