/*
	Adapted to Unreal Angelscript by Embark Studios AB (originally Fredrik Lindh [Temaran]).
	Based on: https://github.com/antlr/grammars-v4/blob/master/cpp/CPP14Lexer.g4
*/

lexer grammar UnrealAngelscriptLexer;

IntegerLiteral:
	DecimalLiteral Integersuffix?
	| OctalLiteral Integersuffix?
	| HexadecimalLiteral Integersuffix?
	| BinaryLiteral Integersuffix?;

CharacterLiteral: ('u' | 'U' | 'L')? '\'' Cchar+ '\'';

FloatingLiteral:
	Fractionalconstant Exponentpart? Floatingsuffix?
	| Digitsequence Exponentpart Floatingsuffix?;

// UnrealAngelscript string literals
// https://angelscript.hazelight.se/scripting/fname-literals/
// https://angelscript.hazelight.se/scripting/format-strings/
fragment Angelscriptstringprefix: 'n' | 'f';

StringLiteral: (Encodingprefix | Angelscriptstringprefix)? (Rawstring | '"' Schar* '"');

BooleanLiteral: False | True;

UserDefinedLiteral:
	UserDefinedIntegerLiteral
	| UserDefinedFloatingLiteral
	| UserDefinedStringLiteral
	| UserDefinedCharacterLiteral;

/*
	Angelscript reserved keywords
	https://www.angelcode.com/angelscript/sdk/docs/manual/doc_reserved_keywords.html
*/

Cast: 'cast';

Import: 'import';

Int: 'int';

Int8: 'int8';

Int16: 'int16';

Int32: 'int32';

Int64: 'int64';

Mixin: 'mixin';

Property: 'property';

UInt: 'uint';

UInt8: 'uint8';

UInt16: 'uint16';

UInt32: 'uint32';

UInt64: 'uint64';

Float: 'float';

Float32: 'float32';

Float64: 'float64';

Double: 'double';

Bool: 'bool';

/* UnrealAngelscript */

UClass: 'UCLASS';

UStruct: 'USTRUCT';

UProperty: 'UPROPERTY';

UFunction: 'UFUNCTION';

UEnum: 'UENUM';

UMeta: 'UMETA';

Ensure: 'ensure';

EnsureAlways: 'ensureAlways';

Check: 'check';

/*Keywords*/

Auto: 'auto';

AcceptTemporaryThis: 'accept_temporary_this';

Access: 'access';

Break: 'break';

Case: 'case';

Catch: 'catch';

Char: 'char';

Class: 'class';

Const: 'const';

Continue: 'continue';

Default: 'default';

Do: 'do';

EditDefaults: 'editdefaults';

Else: 'else';

Enum: 'enum';

Export: 'export';

False: 'false';

Final: 'final';

For: 'for';

Goto: 'goto';

If: 'if';

Inherited: 'inherited';

Namespace: 'namespace';

NoDiscard: 'no_discard';

Nullptr: 'nullptr';

Operator: 'operator';

Override: 'override';

Private: 'private';

Protected: 'protected';

Public: 'public';

ReadOnly: 'readonly';

Return: 'return';

Short: 'short';

Struct: 'struct';

Switch: 'switch';

This: 'this';

True: 'true';

Typedef: 'typedef';

Virtual: 'virtual';

Void: 'void';

While: 'while';

/*Operators*/

LeftParen: '(';

RightParen: ')';

LeftBracket: '[';

RightBracket: ']';

LeftBrace: '{';

RightBrace: '}';

Plus: '+';

Minus: '-';

Star: '*';

Div: '/';

Mod: '%';

Xor: '^^' | '^';

And: '&';

Or: '|';

Tilde: '~';

Not: '!';

Assign: '=';

Less: '<';

Greater: '>';

PlusAssign: '+=';

MinusAssign: '-=';

StarAssign: '*=';

DivAssign: '/=';

ModAssign: '%=';

XorAssign: '^=';

AndAssign: '&=';

OrAssign: '|=';

LeftShiftAssign: '<<=';

RightShiftAssign: '>>=';

Equal: '==';

NotEqual: '!=';

LessEqual: '<=';

GreaterEqual: '>=';

AndAnd: '&&';

OrOr: '||';

PlusPlus: '++';

MinusMinus: '--';

Comma: ',';

Question: '?';

Colon: ':';

Doublecolon: '::';

Semi: ';';

Dot: '.';

fragment Hexquad: HEXADECIMALDIGIT HEXADECIMALDIGIT HEXADECIMALDIGIT HEXADECIMALDIGIT;

fragment Universalcharactername: '\\u' Hexquad | '\\U' Hexquad Hexquad;

Identifier:
	/*
	 Identifiernondigit | Identifier Identifiernondigit | Identifier DIGIT
	 */
	Identifiernondigit (Identifiernondigit | DIGIT)*;

fragment Identifiernondigit: NONDIGIT;

fragment NONDIGIT: [a-zA-Z_];

fragment DIGIT: [0-9];

DecimalLiteral: NONZERODIGIT ('\''? DIGIT)*;

OctalLiteral: '0' ('\''? OCTALDIGIT)*;

HexadecimalLiteral: ('0x' | '0X') HEXADECIMALDIGIT ( '\''? HEXADECIMALDIGIT)*;

BinaryLiteral: ('0b' | '0B') BINARYDIGIT ('\''? BINARYDIGIT)*;

fragment NONZERODIGIT: [1-9];

fragment OCTALDIGIT: [0-7];

fragment HEXADECIMALDIGIT: [0-9a-fA-F];

fragment BINARYDIGIT: [01];

Integersuffix:
	Unsignedsuffix Longsuffix?
	| Unsignedsuffix Longlongsuffix?
	| Longsuffix Unsignedsuffix?
	| Longlongsuffix Unsignedsuffix?;

fragment Unsignedsuffix: [uU];

fragment Longsuffix: [lL];

fragment Longlongsuffix: 'll' | 'LL';

fragment Cchar: ~ ['\\\r\n] | Escapesequence | Universalcharactername;

fragment Escapesequence: Simpleescapesequence | Octalescapesequence | Hexadecimalescapesequence;

fragment Simpleescapesequence:
	'\\\''
	| '\\"'
	| '\\?'
	| '\\\\'
	| '\\a'
	| '\\b'
	| '\\f'
	| '\\n'
	| '\\r'
	| ('\\' ('\r' '\n'? | '\n'))
	| '\\t'
	| '\\v';

fragment Octalescapesequence:
	'\\' OCTALDIGIT
	| '\\' OCTALDIGIT OCTALDIGIT
	| '\\' OCTALDIGIT OCTALDIGIT OCTALDIGIT;

fragment Hexadecimalescapesequence: '\\x' HEXADECIMALDIGIT+;

fragment Fractionalconstant:
	Digitsequence? '.' Digitsequence
	| Digitsequence '.';

fragment Exponentpart:
	'e' SIGN? Digitsequence
	| 'E' SIGN? Digitsequence;

fragment SIGN: [+-];

fragment Digitsequence: DIGIT ('\''? DIGIT)*;

fragment Floatingsuffix: [flFL];

fragment Encodingprefix: 'u8' | 'u' | 'U' | 'L';

fragment Schar: ~ ["\\\r\n] | Escapesequence | Universalcharactername;

fragment Rawstring: 'R"' ( '\\' ["()] | ~[\r\n (])*? '(' ~[)]*? ')' ( '\\' ["()] | ~[\r\n "])*? '"';

UserDefinedIntegerLiteral:
	DecimalLiteral Udsuffix
	| OctalLiteral Udsuffix
	| HexadecimalLiteral Udsuffix
	| BinaryLiteral Udsuffix;

UserDefinedFloatingLiteral:
	Fractionalconstant Exponentpart? Udsuffix
	| Digitsequence Exponentpart Udsuffix;

UserDefinedStringLiteral: StringLiteral Udsuffix;

UserDefinedCharacterLiteral: CharacterLiteral Udsuffix;

fragment Udsuffix: Identifier;

Whitespace: [ \t]+ -> skip;

Newline: ('\r' '\n'? | '\n') -> skip;

BlockComment: '/*' .*? '*/' -> skip;

LineComment: '//' ~ [\r\n]* -> skip;

PreprocessorBranchRemoval: '#else' .*? '#endif' -> skip;

Preprocessor: ('#if' | '#ifdef' | '#else' | '#endif') ~ [\r\n]* -> skip;
