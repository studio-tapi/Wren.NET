// skip: Range subscripts for strings don't currently work.
var a = "string"
a[1.5..2] // expect runtime error: Range start must be an integer.
