namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// GuardHelper
    /// </summary>
    [DebuggerStepThrough]
    public static class GuardHelper
    {
        #region TypeDef

        /// <summary>
        /// ValidatedNotNullAttribute
        /// </summary>
        private sealed class ValidatedNotNullAttribute : Attribute
        {
        }

        #endregion

        /// <summary>
        /// Argument is not null.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ArgumentNotNull([NotNull]object? value, [CallerArgumentExpression("value")] string? argumentExpression = null)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName: argumentExpression);
            }
        }

        /// <summary>
        /// Checks if the the argument  value is not null and returns value, otherwise throws <see cref="ArgumentNullException"/>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <returns>The argument value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ArgumentNotNullValue<T>(T value, [CallerArgumentExpression("value")] string? argumentExpression = null) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName: argumentExpression);
            }

            return value;
        }

        /// <summary>
        /// Determines whether [is argument not default value] [the specified value].
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ArgumentNotDefault<T>(T value, [CallerArgumentExpression("value")] string? argumentExpression = null)
            where T : struct, IEquatable<T>
        {
            if (value.Equals(default(T)))
            {
                throw new ArgumentOutOfRangeException(paramName: argumentExpression);
            }
        }

        /// <summary>
        /// Argument not null and well formed URI.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <param name="uriKind">Kind of the URI.</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ArgumentNotNullAndWellFormedUri(
            string value,
            UriKind uriKind = UriKind.Absolute,
            [CallerArgumentExpression("value")] string? argumentName = null)
        {
            GuardHelper.ArgumentNotNullOrEmpty(value, argumentName);
            if (!Uri.IsWellFormedUriString(value, uriKind))
            {
                throw new ArgumentOutOfRangeException(paramName: argumentName);
            }
        }

        /// <summary>
        /// Argument not null or empty.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">Parameter cannot be an empty string</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ArgumentNotNullOrEmpty([NotNull] string? value, [CallerArgumentExpression("value")] string? argumentName = null)
        {
            GuardHelper.ArgumentNotNull(value, argumentName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentOutOfRangeException(paramName: argumentName, $"Parameter {argumentName} cannot be an empty string");
            }
        }

        /// <summary>
        /// Determines whether the argument value is not null and is not empty
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ArgumentNotNullOrEmpty([ValidatedNotNull] Guid value, [CallerArgumentExpression("value")] string? argumentName = null)
        {
            GuardHelper.ArgumentNotNull(value, argumentName);
            if (value.Equals(Guid.Empty))
            {
                throw new ArgumentOutOfRangeException(paramName: argumentName);
            }
        }

        /// <summary>
        /// Determines whether the argument value is not null and is not empty
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ArgumentNotNullOrEmpty<T>([ValidatedNotNull] ICollection<T> value, [CallerArgumentExpression("value")] string? argumentName = null)
        {
            GuardHelper.ArgumentNotNull(value, argumentName);
            GuardHelper.IsArgumentPositive(value.Count, argumentName);
        }

        /// <summary>
        /// Arguments the not null or empty.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ArgumentNotNullOrEmpty<T>([ValidatedNotNull] IEnumerable<T> value, [CallerArgumentExpression("value")] string? argumentName = null)
        {
            GuardHelper.ArgumentNotNull(value, argumentName);
            if (!value.Any())
            {
                throw new ArgumentOutOfRangeException(paramName: argumentName);
            }
        }

        /// <summary>
        /// Determines whether the argument value is valid Guid.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentValidGuid([NotNull] string value, [CallerArgumentExpression("value")] string? argumentName = null)
        {
            GuardHelper.ArgumentNotNullOrEmpty(value, argumentName);
            if (!value.SafeIsValidGuid())
            {
                throw new ArgumentException($"Argument {argumentName}({value}) is not a valid Guid");
            }
        }

        /// <summary>
        /// Determines whether the argument value is null or set to default for given type
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentNotNullOrDefault<T>(T? value, [CallerArgumentExpression("value")] string? argumentName = null)
             where T : struct, IEquatable<T>
        {
            if (value == null || value.Value.Equals(default(T)))
            {
                throw new ArgumentNullException(paramName: argumentName);
            }
        }

        /// <summary>
        /// Determines whether [is argument greater than] [the specified LHS].
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lhs">LHS.</param>
        /// <param name="rhs">RHS.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentGreaterThan<T>(
            T lhs,
            T rhs,
            [CallerArgumentExpression("lhs")] string? lhsArgumentName = null,
            [CallerArgumentExpression("rhs")] string? rhsArgumentName = null)
            where T : IComparable<T>
        {
            if (lhs.CompareTo(rhs) <= 0)
            {
                throw new ArgumentOutOfRangeException(paramName: $"{lhsArgumentName}({lhs}) is not greaterthan {rhsArgumentName}({rhs})");
            }
        }

        /// <summary>
        /// Determines whether [is argument greater than or equal] [the specified LHS].
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lhs">The LHS.</param>
        /// <param name="rhs">The RHS.</param>
        /// <param name="lhsArgumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentGreaterThanOrEqual<T>(
            T lhs,
            T rhs,
            [CallerArgumentExpression("lhs")] string? lhsArgumentName = null,
            [CallerArgumentExpression("rhs")] string? rhsArgumentName = null)
            where T : IComparable<T>
        {
            if (lhs.CompareTo(rhs) < 0)
            {
                throw new ArgumentOutOfRangeException(paramName: $"{lhsArgumentName}({lhs}) is not greaterthan or equal to {rhsArgumentName}({rhs})");
            }
        }

        /// <summary>
        /// Determines whether [is argument equal] [the specified LHS].
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lhs">The LHS.</param>
        /// <param name="rhs">The RHS.</param>
        /// <param name="lhsArgumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentEqual<T>(
            T lhs,
            T rhs,
            [CallerArgumentExpression("lhs")] string? lhsArgumentName = null,
            [CallerArgumentExpression("rhs")] string? rhsArgumentName = null)
            where T : IEquatable<T>
        {
            if (!lhs.Equals(rhs))
            {
                throw new ArgumentOutOfRangeException(paramName: $"{lhsArgumentName}({lhs}) is not equal to {rhsArgumentName}({rhs})");
            }
        }

        /// <summary>
        /// Determines whether [is argument not equal] [the specified LHS].
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lhs">The LHS.</param>
        /// <param name="rhs">The RHS.</param>
        /// <param name="lhsArgumentName">Name of the argument.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentNotEqual<T>(
            T lhs,
            T rhs,
            [CallerArgumentExpression("lhs")] string? lhsArgumentName = null,
            [CallerArgumentExpression("rhs")] string? rhsArgumentName = null)
            where T : IEquatable<T>
        {
            if (lhs.Equals(rhs))
            {
                throw new ArgumentOutOfRangeException(paramName: $"{lhsArgumentName}({lhs}) is equal to {rhsArgumentName}({rhs})");
            }
        }

        /// <summary>
        /// Determines whether the argument value is non zero
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentPositive([ValidatedNotNull] int value, [CallerArgumentExpression("value")] string? argumentName = null)
        {
            GuardHelper.IsArgumentGreaterThan(value, 0, argumentName);
        }

        /// <summary>
        /// Determines whether the argument value is non zero
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentPositive([ValidatedNotNull] long value, [CallerArgumentExpression("value")] string? argumentName = null)
        {
            GuardHelper.IsArgumentGreaterThan(value, 0, argumentName);
        }

        /// <summary>
        /// Determines whether the argument value is non zero
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentPositive([ValidatedNotNull] double value, [CallerArgumentExpression("value")] string? argumentName = null)
        {
            GuardHelper.IsArgumentGreaterThan(value, 0, argumentName);
        }

        /// <summary>
        /// Determines whether [is argument within range] [the specified value].
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="min">The minimum.</param>
        /// <param name="max">The maximum.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        /// TODO: Make generic
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentWithinRange(int value,
            int min,
            int max,
            [CallerArgumentExpression("value")] string? argumentName = null,
            [CallerArgumentExpression("min")] string? minArgumentName = null,
            [CallerArgumentExpression("max")] string? maxArgumentName = null)
        {
            if (min > value || value > max)
            {
                throw new ArgumentOutOfRangeException($"{argumentName} is not in range from {minArgumentName}({min}) to {maxArgumentName}({max})");
            }
        }

        /// <summary>
        /// Determines whether [is arugment within range inclusive] [the specified value].
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <param name="min">The minimum.</param>
        /// <param name="max">The maximum.</param>
        /// <param name="argumentName">Name of the argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentWithinRangeInclusive<T>(
            T value,
            T min,
            T max,
            [CallerArgumentExpression("value")] string? argumentName = null)
            where T : IComparable<T>
        {
            IsArgumentGreaterThanOrEqual(value, min, argumentName);
            IsArgumentGreaterThanOrEqual(max, value, argumentName);
        }

        /// <summary>
        /// Determines whether [is argument nonnegative] [the specified value].
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentNonNegative([ValidatedNotNull] int value, [CallerArgumentExpression("value")] string? argumentName = null)
        {
            GuardHelper.IsArgumentGreaterThanOrEqual(value, 0, argumentName);
        }

        /// <summary>
        /// Determines whether [is argument nonnegative] [the specified value].
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentNonNegative([ValidatedNotNull] long value, [CallerArgumentExpression("value")] string? argumentName = null)
        {
            GuardHelper.IsArgumentGreaterThanOrEqual(value, 0, argumentName);
        }

        /// <summary>
        /// Determines whether [is argument non negative] [the specified value].
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentNonNegative([ValidatedNotNull] double value, [CallerArgumentExpression("value")] string? argumentName = null)
        {
            GuardHelper.IsArgumentGreaterThanOrEqual(value, 0, argumentName);
        }

        /// <summary>
        /// Determine whether [is argument defined enum value] [the specified value]. 
        /// </summary>
        /// <param name="enumType">The enum type.</param>
        /// <param name="value">>The value.</param>
        /// <param name="argumentName">Name of the argument.</param>
        /// <exception cref="System.ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsArgumentDefinedEnumValue(
            Type enumType,
            object value,
            [CallerArgumentExpression("value")] string? argumentName = null)
        {
            if (!Enum.IsDefined(enumType, value))
            {
                throw new ArgumentException("Argument is not a valid enum value", argumentName);
            }
        }

        /// <summary>
        /// Determines if the argument satisfies a given constraint.
        /// </summary>
        /// <param name="constraintLambda">Constraint lambda.</param>
        /// <param name="argumentName">Argument name</param>
        /// <param name="constraintMessage">Constraint message</param>
        /// <exception cref="InvalidConstraintException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ArgumentConstraintCheck(bool constraintPassed, [CallerArgumentExpression("constraintPassed")] string? constraintFailedMessage = null)
        {
            ArgumentNotNullOrEmpty(constraintFailedMessage, nameof(constraintFailedMessage));

            if (!constraintPassed)
            {
                throw new InvalidConstraintException(constraintFailedMessage);
            }
        }

        /// <summary>
        /// Check nullity of object and returns it.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T NonNullObject<T>(T value, [CallerArgumentExpression("value")] string? argumentName = null)
        {
            ArgumentNotNull(value, nameof(value));
            return value;
        }
    }
}