# Game Logger Usage

This logger is intended to act as a single point of entry to writing to logs

Logs follow the same format.

Call GameLogger as follows:

```cs
GameLogger.Log();
GameLogger.Warning();
GameLogger.Error();
```

Pass in the calling class and the desired message.

Logging can be disabled for a class by adding it to its disabled list using:

`disabledInstances`
