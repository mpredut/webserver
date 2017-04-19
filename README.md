# webserver
 http plain web server
   1.Am integrat un proiect de pe net de parsat http request(https://github.com/bvanderveen/httpmachine)
              (din pacate mai are buguri in special pt. POST)
     2.Am adaugat suport pt. Keep Alive + alte taguri.
     3.Am venit cu un sistem de workrpool si  pt. memorie, contexte nu numai pt. socketi.
     4.Am pastrat sistemul asincron de socketi(workerpool-ul transparent din dot.net pt socketi)
     5.Am venit cu suport pt. POST.
     6.Am curatat codul din punct de vedere al stilului.

Am facut teste de benchmarck (din pacate si asta cam are buguri )
http://www.devside.net/wamp-server/load-testing-apache-with-ab-apache-bench
si sa comportat bine(pt. un "ab.exe -n 100000 -c 1024 -r -k http://127.0.0.1/" am avut in jur de 2000 de requests pe secunda)
