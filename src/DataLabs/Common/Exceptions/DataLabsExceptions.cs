namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Exceptions
{
    using System;

    public class ChainedPrevTaskFailedException : Exception
    {
        public ChainedPrevTaskFailedException(string message) : base(message)
        {
        }
    }

    public class ProcessNotMovedTaskException : Exception
    {
        public ProcessNotMovedTaskException(string message) : base(message)
        {
        }
    }

    public class InvalidFinalStageException : Exception
    {
        public InvalidFinalStageException(string message) : base(message)
        {
        }
    }

    public class NotAllowedOutputResourceTypeException : Exception
    {
        public NotAllowedOutputResourceTypeException(string message) : base(message)
        {
        }
    }

    public class ResourceTypeNotOnboardedException : Exception
    {
        public ResourceTypeNotOnboardedException(string message) : base(message)
        {
        }
    }

    public class PartnerSentErrorResponseException : Exception
    {
        public PartnerSentErrorResponseException(string message) : base(message)
        {
        }
    }

    public class NotAllowedPartnerResponseException : Exception
    {
        public NotAllowedPartnerResponseException(string message) : base(message)
        {
        }
    }

    public class StreamChildTaskFailedException : Exception
    {
        public StreamChildTaskFailedException(string message) : base(message)
        {
        }
    }

    public class NoResourceInNotificationException : Exception
    {
        public NoResourceInNotificationException(string message) : base(message)
        {
        }

    }

    public class OutputCacheDeleteFailException : Exception
    {
        public OutputCacheDeleteFailException(string message) : base(message)
        {
        }

    }

    public class AddToCacheFailedException : Exception
    {
        public AddToCacheFailedException(string message) : base(message)
        {
        }

    }

    public class AddNotFoundToCacheFailedException : Exception
    {
        public AddNotFoundToCacheFailedException(string message) : base(message)
        {
        }
    }

    public class NotAllowedTypeException : Exception
    {
        public NotAllowedTypeException(string message) : base(message)
        {
        }
    }

    public class ConvertDeleteToNotFoundException : Exception
    {
        public ConvertDeleteToNotFoundException(string message) : base(message)
        {
        }
    }

    public class ProxyReturnErrorResponseException : Exception
    {
        public ProxyReturnErrorResponseException(string message) : base(message)
        {
        }
    }

    public class ThrottleExistInCacheException : Exception
    {
        public ThrottleExistInCacheException(string message) : base(message)
        {
        }
    }

    public class NotValidResponseStatusCodeException : Exception
    {
        public NotValidResponseStatusCodeException(string message) : base(message)
        {
        }
    }

    public class NotFoundEntryExistInCacheException : Exception
    {
        public NotFoundEntryExistInCacheException(string message) : base(message)
        {
        }
    }

    public class NotAllowedPartnerException : Exception
    {
        public NotAllowedPartnerException(string message) : base(message)
        {
        }
    }

    public class NotAllowedClientIdException : Exception
    {
        public NotAllowedClientIdException(string message) : base(message)
        {
        }
    }

    public class ResourceFetcherBadRequestException : Exception
    {
        public ResourceFetcherBadRequestException(string message) : base(message)
        {
        }
    }

    public class NoPartnerInRequestException : Exception
    {
        public NoPartnerInRequestException(string message) : base(message)
        {
        }
    }

    public class NoTokenInHeaderException : Exception
    {
        public NoTokenInHeaderException(string message) : base(message)
        {
        }
    }

    public class AADTokenAuthFailException : Exception
    {
        public AADTokenAuthFailException(string message) : base(message)
        {
        }
    }

    public class CacheInsertionFailException : Exception
    {
        public CacheInsertionFailException(string message) : base(message)
        {
        }
    }

    public class CacheCollisionException : Exception
    {
        public CacheCollisionException(string message) : base(message)
        {
        }
    }
}
