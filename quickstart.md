# Quick Start Guide

Broccoli is an exciting new programming language that is guaranteed to make you pee your
pants (in a good way).

*this guide can be found online at http://www.earthvssoup.com/projects/broccoli/quickstart.html*

{menu: {menuStyle: section, options: {maxLevels: 4, numberSections: false}}}

## Getting Started
Get the executable:

- [Email me with your desired OS target](mailto:broccoli@earthvssoup.com "email")

Quick Start:

0. Unpack the Broccoli binary
1. To run Broccoli's REPL type the following `./broccoli`
2. To see a list of commands available type `(help)`

## Data Types
Broccoli supports the following datatypes:

- integer (mapped to 64-bit long)
- floating point (mapped to double precision float)
- boolean (t, nil)
- atom
- string
- lists

## Variables
Scalar variables (e.g. int, float, boolean, atom, string) in Broccoli take the form $name
Collection variables (e.g. lists) take the form @name



## Core Commands

### Meta-commands
#### `(help)`
Shows the list of available commands and keywords.

#### `(quit)`
Quits the Broccoli REPL

#### `(clear)`
Clears the Broccoli REPL's internal memory of all elements.

#### `(reset)`
Clears the Broccoli REPL's internal memory of all elements, *except* structure definitions.

#### `(eval)`
Takes a string and evaluates it as if it was typed into the command prompt.

#### `(call)`
Takes a function name and 0<sup>*</sup> arguments and *calls* the former with the latter.

`(call + 2 3)`  => 5

#### `(run)`
Takes a string pertaining to an external file and evaluates its contents as if they had been typed
into the command prompt.

#### `(import)`
Takes a string pertaining to an external file and evaluates only its constructs (i.e. functions and bindings).

#### `(bench)`
Takes any validate Broccoli expression and times its execution, returning the number of seconds it took to execute.

### I/O Commands
#### `(print)`
Prints text to the console by default.  The print function takes 1 or more arguments, including
the special symbols *endl* and *tab*.

*See what happens when you type the following into the REPL:*

`(print "This is the number two" tab 2 endl)`

`(print "This is also the number two" tab (/ 100 50) endl)`

`(:= $two 2)`

`(print "This is the number two" tab $two endl)`



### List commands
#### `(list)`
Creates a list of the arguments given.

#### `(len)`
Returns the number of items in a list

#### `(first)`
Given a list, returns a list of its first element

#### `(rest)`
Given a list, returns a list of all **but** its first element

#### `(slice)`
Given a list, a start index, and an end index, returns a sub-list slice of the original

#### `(range)`
Generates a list containing the fill of the range given

*Example:*

`(range -5 2)`  => (-5 -4 -3 -2 -1 0 1 2)

#### `(cat)`
Concatenates two or more lists

*Example:*

`(cat (list a b c) (list 1 2 3))`  => (a b c 1 2 3)



### Basic Math
#### `(+) (*)`
Add, multiply.  Each takes 0 or more arguments.

#### `(-) (/)`
Subtract, Divide.  Each takes 1 or more arguments.

#### `(:=)`
Assignment operator of the form `(:= $var <expression>)`.

*Example:*
`(:= $pi 3.14)`

#### `(int) (float)`
Converts a number to either an integer or float respectively.



### Comparison
#### `(=)`
Checks if the first value is equal to every other value.

#### `(/=)`
Checks if the first value is not equal to every other value.

### Logic
#### `(not)`
Reverses the truth value of a boolean value.

#### `(and)`
Logical and of zero or more truth values.

#### `(or)`
Logical or of zero or more truth values.



### Flow control
#### `(for)`
Iterates through the contents of a list

*Example*
<pre><code>
(for $e in (list a b c)
    (print $e endl)
)
</code></pre>

In order to iterate a given number of times, the `(for)` loop can be combined with the `(range)` function:

*Example*
<pre><code>
(for $e in (range 0 5)
    (print $e endl)
)
</code></pre>

#### `(if)`
The basic if-then statement.

*Example*
<pre><code>
(if (t /= nil)
    (print Always prints endl)
 else
    (print Never prints endl)
)
</code></pre>

## broccoli.brocc

The broccoli.broc file contains extended functions written in the Broccoli language and loaded (when present) at the interpreter's start.

### Extended functions
#### `(fib)`
Takes a number and returns its [Fibonacci number](http://en.wikipedia.org/wiki/Fibonacci_number).

#### `(fact)`
Calculates the factorial of a given number.

#### `(map)`
Takes a function name and a list and applies it to each element, returning a list of the results.

*Example*

`(map fib (list 1 2 3 4 5))`  => (1 1 2 3 5)

#### `(reduce)`
Takes a function name and a list and reduces the list according to the function, returning an aggregate value.

*Example*

`(reduce + (list 1 2 3 4 5))`  => 15

#### `(filter)`
Takes a function name and a list and filters out the values that the function returns false for.

*Example*

`(fn gt10 ($num) (if (< 10 $num) t else nil))`

`(filter gt10 (list 1 2 10 11 23 6 9 1000))`  => (1000 23 11)
